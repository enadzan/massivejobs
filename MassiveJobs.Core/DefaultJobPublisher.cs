using System;
using System.Collections.Generic;

namespace MassiveJobs.Core
{
    public class DefaultJobPublisher : IJobPublisher
    {
        protected const int BatchPublishTimeoutMs = 5 * 1000;

        private readonly ushort _batchSize;

        private int _nextImmediateWorkerIndex;
        private int _nextScheduledWorkerIndex;

        protected readonly ILogger Logger;
        protected readonly MassiveJobsSettings Settings;
        protected readonly IMessagePublisher MessagePublisher;
        protected readonly IJobTypeProvider JobTypeProvider;
        protected readonly IJobSerializer JobSerializer;

        public DefaultJobPublisher(MassiveJobsSettings settings, 
            IMessagePublisher messagePublisher, 
            IJobTypeProvider jobTypeProvider, 
            IJobSerializer jobSerializer, 
            ILogger logger) 
        {
            _batchSize = settings.PublishBatchSize;

            Settings = settings;
            MessagePublisher = messagePublisher;
            JobTypeProvider = jobTypeProvider;
            JobSerializer = jobSerializer;
            Logger = logger;
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

            foreach (var kvp in jobsPerKey)
            {
                var routingKey = kvp.Key;
                var batch = kvp.Value;

                PublishJobs(batch, routingKey);
            }
        }

        protected virtual string GetRoutingKey(JobInfo jobInfo, DateTime now)
        {
            if (jobInfo.Retries.HasValue)
            {
                return jobInfo.Retries.Value >= 25 ? Settings.FailedQueueName : Settings.ErrorQueueName;
            }

            if (jobInfo.PeriodicRunInfo != null)
            {
                var workerIndex = jobInfo.GetGroupKeyHashCode() % Settings.PeriodicWorkersCount;
                return FormatRoutingKey(Settings.PeriodicQueueNameTemplate, Settings.PeriodicWorkersCount, ref workerIndex);
            }

            if (jobInfo.RunAtUtc > now)
            {
                return FormatRoutingKey(Settings.ScheduledQueueNameTemplate, Settings.ScheduledWorkersCount, ref _nextScheduledWorkerIndex);
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

            var batchCount = 0;

            foreach (var job in jobs)
            {
                var serializedJob = JobSerializer.Serialize(job, JobTypeProvider);

                MessagePublisher.Publish(routingKey, serializedJob, JobTypeProvider.TypeToTag(job.ArgsType), true);

                batchCount++;

                if (batchCount >= _batchSize)
                {
                    MessagePublisher.WaitForConfirmsOrDie(TimeSpan.FromMilliseconds(BatchPublishTimeoutMs));
                    batchCount = 0;
                }
            }

            if (batchCount > 0)
            {
                MessagePublisher.WaitForConfirmsOrDie(TimeSpan.FromMilliseconds(BatchPublishTimeoutMs));
            }
        }

        /*
        protected void EnsurePublishersExist()
        {
            if (PublishersPool != null && PublishersPool.AllOk()) return;

            DisposePublishers();
            
            try
            {
                MessageBroker = Settings.MessageBrokerFactory.CreateMessageBroker(MessageBrokerType.JobPublisher);
                MessageBroker.DeclareTopology();

                PublishersPool = new PublishersPool(MessageBroker, 2);

                OnMessageBrokerCreated();
            }
            catch
            {
                DisposePublishers();
                throw;
            }
        }

        protected void DisposePublishers()
        {
            PublishersPool.SafeDispose();
            PublishersPool = null;

            MessageBroker.SafeDispose();
            MessageBroker = null;
        }

        protected virtual void OnMessageBrokerCreated()
        {
        }

        */
    }
}
