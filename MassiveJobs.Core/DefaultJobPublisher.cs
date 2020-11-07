using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace MassiveJobs.Core
{
    public class DefaultJobPublisher : IJobPublisher
    {
        protected const int BatchPublishTimeoutMs = 5 * 1000;

        private readonly ushort _batchSize;

        private int _nextImmediateWorkerIndex;
        private int _nextScheduledWorkerIndex;

        private int _nextLongRunningImmediateWorkerIndex;
        private int _nextLongRunningScheduledWorkerIndex;

        protected readonly IJobLogger Logger;
        protected readonly MassiveJobsSettings Settings;
        protected readonly IMessagePublisher MessagePublisher;
        protected readonly IJobTypeProvider JobTypeProvider;
        protected readonly IJobSerializer JobSerializer;

        public DefaultJobPublisher(MassiveJobsSettings settings, 
            IMessagePublisher messagePublisher, 
            IJobTypeProvider jobTypeProvider, 
            IJobSerializer jobSerializer, 
            IJobLogger logger) 
        {
            _batchSize = settings.PublishBatchSize;

            Settings = settings;
            MessagePublisher = messagePublisher;
            JobTypeProvider = jobTypeProvider;
            JobSerializer = jobSerializer;
            Logger = logger;

            // This is just to avoid always publishing to a first worker 
            // since the new instance of publisher is created on each job batch
            // for scheduled/periodic workers. Actually, immediate workers will
            // also create one instance of publisher per batch to inject into
            // jobs if jobs require it in their constructors.

            var tickCount = Math.Abs(Environment.TickCount);

            if (settings.ImmediateWorkersCount > 0) _nextImmediateWorkerIndex = tickCount % settings.ImmediateWorkersCount;
            if (settings.ScheduledWorkersCount > 0) _nextScheduledWorkerIndex = tickCount % settings.ScheduledWorkersCount;
            if (settings.LongRunningImmediateWorkersCount > 0) _nextLongRunningImmediateWorkerIndex = tickCount % settings.LongRunningImmediateWorkersCount;
            if (settings.LongRunningScheduledWorkersCount > 0) _nextLongRunningScheduledWorkerIndex = tickCount % settings.LongRunningScheduledWorkersCount;
        }

        public virtual void Dispose()
        {
        }

        public void Publish(IEnumerable<JobInfo> jobs)
        {
            var jobsPerKey = new Dictionary<string, List<JobInfo>>();

            var now = DateTime.UtcNow;

            foreach (var jobInfo in jobs)
            {
                var routingKey = GetRoutingKey(jobInfo, now);

                if (!jobsPerKey.TryGetValue(routingKey, out var jobList))
                {
                    jobList = new List<JobInfo>();
                    jobsPerKey.Add(routingKey, jobList);
                }

                jobList.Add(jobInfo);
            }

            var tasks = new Task[jobsPerKey.Count];

            var index = 0;
            foreach (var kvp in jobsPerKey)
            {
                var routingKey = kvp.Key;
                var batch = kvp.Value;

                tasks[index++] = Task.Run(() => PublishJobs(batch, routingKey));
            }

            Task.WaitAll(tasks);
        }

        protected virtual string GetRoutingKey(JobInfo jobInfo, DateTime now)
        {
            if (jobInfo.HasErrors)
            {
                return jobInfo.Retries >= 25 ? Settings.FailedQueueName : Settings.ErrorQueueName;
            }

            const int longRunningThresholdMs = 10_000; // 10 sec;

            if (jobInfo.PeriodicRunInfo != null)
            {
                int workerIndex;
                if (jobInfo.TimeoutMs >= longRunningThresholdMs)
                {
                    workerIndex = Math.Abs(jobInfo.GetGroupKeyHashCode()) % Settings.LongRunningScheduledWorkersCount;
                    return FormatRoutingKey(Settings.LongRunningScheduledQueueNameTemplate, Settings.LongRunningScheduledWorkersCount, ref workerIndex);
                }

                workerIndex = Math.Abs(jobInfo.GetGroupKeyHashCode()) % Settings.ScheduledWorkersCount;
                return FormatRoutingKey(Settings.ScheduledQueueNameTemplate, Settings.ScheduledWorkersCount, ref workerIndex);
            }

            if (jobInfo.RunAtUtc > now)
            {
                if (jobInfo.TimeoutMs >= longRunningThresholdMs)
                {
                    return FormatRoutingKey(Settings.LongRunningScheduledQueueNameTemplate,
                        Settings.LongRunningScheduledWorkersCount, ref _nextLongRunningScheduledWorkerIndex);
                }

                return FormatRoutingKey(Settings.ScheduledQueueNameTemplate, Settings.ScheduledWorkersCount, ref _nextScheduledWorkerIndex);
            }

            if (jobInfo.TimeoutMs >= longRunningThresholdMs)
            {
                return FormatRoutingKey(Settings.LongRunningImmediateQueueNameTemplate, 
                    Settings.LongRunningImmediateWorkersCount, ref _nextLongRunningImmediateWorkerIndex);
            }

            return FormatRoutingKey(Settings.ImmediateQueueNameTemplate, Settings.ImmediateWorkersCount, ref _nextImmediateWorkerIndex);
        }

        protected string FormatRoutingKey(string template, int workersCount, ref int workerIndex)
        {
            if (workersCount <= 0) throw new Exception($"Cannot publish a job in a queue with template {template} when workers count is 0!");

            var routingKey = string.Format(template, workerIndex);
            workerIndex = (workerIndex + 1) % workersCount;
            return routingKey;
        }

        protected void PublishJobs(IReadOnlyList<JobInfo> jobs, string routingKey)
        {
            if (jobs.Count == 0) return;

            var batch = new List<RawMessage>();

            foreach (var job in jobs)
            {
                var serializedJob = JobSerializer.Serialize(job, JobTypeProvider);

                batch.Add(new RawMessage
                {
                    TypeTag = JobTypeProvider.TypeToTag(job.ArgsType),
                    Body = serializedJob.ToArray(),
                    IsPersistent = true
                });

                if (batch.Count >= _batchSize)
                {
                    MessagePublisher.Publish(routingKey, batch, TimeSpan.FromMilliseconds(BatchPublishTimeoutMs));
                    batch.Clear();
                }
            }

            if (batch.Count > 0)
            {
                MessagePublisher.Publish(routingKey, batch, TimeSpan.FromMilliseconds(BatchPublishTimeoutMs));
            }
        }
    }
}
