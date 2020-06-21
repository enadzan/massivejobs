using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace MassiveJobs.Core
{
    public class DefaultJobPublisher : IJobPublisher
    {
        private const int StatsTimerPeriodSec = 5;
        protected const int BatchPublishTimeoutMs = 5 * 1000;

        private readonly ushort _batchSize;

        private readonly StatsMessage _stats;
        private readonly Timer _statsTimer;

        private int _nextImmediateWorkerIndex;
        private int _nextScheduledWorkerIndex;

        protected readonly ILogger Logger;
        protected readonly MassiveJobsSettings Settings;
        protected readonly object PublishersLock = new object();

        protected WorkerCoordinator WorkerCoordinator;
        protected IMessageBroker MessageBroker;
        protected PublishersPool PublishersPool;

        public DefaultJobPublisher(MassiveJobsSettings settings)
            : this(settings, settings.LoggerFactory.SafeCreateLogger<DefaultJobPublisher>())
        {
            WorkerCoordinator = new WorkerCoordinator(this, settings);
        }

        protected DefaultJobPublisher(MassiveJobsSettings settings, ILogger logger) 
        {
            _batchSize = settings.PublishBatchSize;
            _stats = new StatsMessage
            {
                PublisherId = Guid.NewGuid().ToString(),
                Stats = new Dictionary<string, string>
                {
                    { "::masivejobs::machine_name", Environment.MachineName }
                }
            };

            Settings = settings;
            Logger = logger;

            _statsTimer = new Timer(PublishStats, null, StatsTimerPeriodSec * 1000, Timeout.Infinite);
        }

        public virtual void Dispose()
        {
            WorkerCoordinator.SafeDispose();

            lock (PublishersLock)
            {
                DisposePublishers();
            }
        }

        public void StartJobWorkers()
        {
            WorkerCoordinator.StartWorkers();
        }

        public void StopJobWorkers()
        {
            WorkerCoordinator.StopWorkers();
        }

        public void Publish(IEnumerable<JobInfo> jobs)
        {
            lock (PublishersLock)
            {
                EnsurePublishersExist();

                try
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
                    var taskIndex = 0;

                    foreach (var kvp in jobsPerKey)
                    {
                        var routingKey = kvp.Key;
                        var batch = kvp.Value;

                        tasks[taskIndex++] = Task.Run(() => PublishJobs(batch, routingKey));
                    }

                    Task.WaitAll(tasks);
                }
                catch
                {
                    DisposePublishers();
                    throw;
                }
            }
        }

        internal void ReportStats(IReadOnlyDictionary<string, string> stats)
        {
            lock (PublishersLock)
            {
                foreach (var kvp in stats)
                {
                    _stats.Stats[kvp.Key] = kvp.Value;
                }
            }
        }

        protected virtual string GetRoutingKey(JobInfo jobInfo, DateTime now)
        {
            if (jobInfo.Retries.HasValue)
            {
                return jobInfo.Retries.Value >= 25 ? Settings.FailedQueueName : Settings.ErrorQueueName;
            }

            if (jobInfo.RunAtUtc > now)
            {
                return FormatRoutingKey(Settings.ScheduledQueueNameTemplate, Settings.ScheduledWorkersCount, ref _nextScheduledWorkerIndex);
            }

            return FormatRoutingKey(Settings.ImmediateQueueNameTemplate, Settings.ImmediateWorkersCount, ref _nextImmediateWorkerIndex);
        }

        protected string FormatRoutingKey(string template, int workersCount, ref int workerIndex)
        {
            var routingKey = string.Format(template, workerIndex);
            workerIndex = (workerIndex + 1) % workersCount;
            return routingKey;
        }

        private void PublishStats(object state)
        {
            try
            {
                lock (PublishersLock)
                {
                    EnsurePublishersExist();

                    var publisher = PublishersPool.Get();
                    try
                    {

                        publisher.Publish(
                            Settings.StatsQueueName,
                            DefaultSerializer.SerializeObject(_stats),
                            Settings.TypeProvider.TypeToTag(typeof(StatsMessage)),
                            false
                        );

                        publisher.WaitForConfirmsOrDie(TimeSpan.FromMilliseconds(BatchPublishTimeoutMs));
                        PublishersPool.Return(publisher);
                    }
                    catch
                    {
                        PublishersPool.Return(publisher);
                        DisposePublishers();
                        throw;
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error in publishling statistics");
            }
            finally
            {
                _statsTimer.Change(StatsTimerPeriodSec * 1000, Timeout.Infinite);
            }
        }

        protected void PublishJobs(IReadOnlyList<JobInfo> jobs, string routingKey)
        {
            if (jobs.Count == 0) return;

            var publisher = PublishersPool.Get();
            try
            {
                var batchCount = 0;

                foreach (var job in jobs)
                {
                    PublishJob(job, publisher, routingKey, Settings.Serializer, Settings.TypeProvider);

                    batchCount++;

                    if (batchCount >= _batchSize)
                    {
                        publisher.WaitForConfirmsOrDie(TimeSpan.FromMilliseconds(BatchPublishTimeoutMs));
                        batchCount = 0;
                    }
                }

                if (batchCount > 0)
                {
                    publisher.WaitForConfirmsOrDie(TimeSpan.FromMilliseconds(BatchPublishTimeoutMs));
                }
            }
            finally
            {
                PublishersPool.Return(publisher);
            }
        }

        private static void PublishJob(
            JobInfo job,
            IMessagePublisher publisher, 
            string routingKey,
            IJobSerializer serializer,
            IJobTypeProvider typeProvider) 
        {
            var serializedJob = serializer.Serialize(job, typeProvider);

            publisher.Publish(
                routingKey,
                serializedJob,
                typeProvider.TypeToTag(job.ArgsType),
                true
            );
        }

        protected void EnsurePublishersExist()
        {
            if (PublishersPool != null && PublishersPool.AllOk()) return;

            DisposePublishers();
            
            try
            {
                MessageBroker = Settings.MessageBrokerFactory.CreateMessageBroker();
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
    }
}
