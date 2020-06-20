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
        protected readonly IList<IMesagePublisher> MessagePublishers;

        protected readonly Dictionary<int, List<JobInfo>> Jobs = new Dictionary<int, List<JobInfo>>();

        protected WorkerCoordinator WorkerCoordinator;
        protected IMessageBroker MessageBroker;

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
            MessagePublishers = new List<IMesagePublisher>();

            Jobs.Add((int)JobType.Immediate, new List<JobInfo>());
            Jobs.Add((int)JobType.Scheduled, new List<JobInfo>());
            Jobs.Add((int)JobType.Error, new List<JobInfo>());
            Jobs.Add((int)JobType.Failed, new List<JobInfo>());

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
                    foreach (var jobsList in Jobs.Values)
                    {
                        jobsList.Clear();
                    }

                    var now = DateTime.UtcNow;

                    foreach (var jobInfo in jobs)
                    {
                        var jobType = GetJobType(jobInfo, now);

                        if (!Jobs.TryGetValue(jobType, out var jobList))
                        {
                            throw new ArgumentOutOfRangeException($"Unknown job type: {jobType}");
                        }

                        jobList.Add(jobInfo);
                    }

                    PublishBatch(Jobs[(int)JobType.Immediate], Settings.ImmediateQueueNameTemplate, Settings.ImmediateWorkersCount, ref _nextImmediateWorkerIndex);
                    PublishBatch(Jobs[(int)JobType.Scheduled], Settings.ScheduledQueueNameTemplate, Settings.ScheduledWorkersCount, ref _nextScheduledWorkerIndex);

                    var errorWorkerIndex = 0;
                    PublishBatch(Jobs[(int)JobType.Error], Settings.ErrorQueueName, 1, ref errorWorkerIndex);

                    var failedWorkerIndex = 0;
                    PublishBatch(Jobs[(int)JobType.Failed], Settings.FailedQueueName, 1, ref failedWorkerIndex);

                    PostProcessJobs();
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

        protected virtual void PostProcessJobs()
        {
        }

        protected virtual int GetJobType(JobInfo jobInfo, DateTime now)
        {
            if (jobInfo.Retries.HasValue)
            {
                return jobInfo.Retries.Value >= 25 ? (int)JobType.Failed : (int)JobType.Error;
            }

            return jobInfo.RunAtUtc > now ? (int)JobType.Scheduled : (int)JobType.Immediate;
        }

        private void PublishStats(object state)
        {
            try
            {
                lock (PublishersLock)
                {
                    EnsurePublishersExist();

                    try
                    {
                        var publisher = MessagePublishers[0];

                        publisher.Publish(
                            Settings.ExchangeName,
                            Settings.StatsQueueName,
                            DefaultSerializer.SerializeObject(_stats),
                            Settings.TypeProvider.TypeToTag(typeof(StatsMessage)),
                            false
                        );

                        publisher.WaitForConfirmsOrDie(TimeSpan.FromMilliseconds(BatchPublishTimeoutMs));
                    }
                    catch
                    {
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

        private void PublishBatch(IReadOnlyList<JobInfo> jobs, string routingKeyTemplate, int workersCount, ref int workerIndex)
        {
            if (workersCount <= 0) throw new ArgumentOutOfRangeException(nameof(workersCount));
            if (jobs.Count == 0) return;

            var batches = new List<JobInfo>[workersCount];

            for(var i = 0; i < batches.Length; i++)
            {
                batches[i] = new List<JobInfo>();
            }

            foreach (var job in jobs)
            {
                workerIndex = (workerIndex + 1) % workersCount;
                batches[workerIndex].Add(job);
            }

            var tasks = new Task[batches.Length];
            for (var i = 0; i < batches.Length; i++)
            {
                var modelIndex = i;
                tasks[i] = Task.Run(() =>
                {
                    var routingKey = string.Format(routingKeyTemplate, modelIndex);
                    PublishWorkerBatch(batches[modelIndex], modelIndex, routingKey);
                });
            }

            Task.WaitAll(tasks);
        }

        protected void PublishWorkerBatch(IReadOnlyList<JobInfo> jobs, int modelIndex, string routingKey)
        {
            if (jobs.Count == 0) return;

            var publisher = MessagePublishers[modelIndex];
            var batchCount = 0;

            foreach (var job in jobs)
            {
                PublishJob(job, publisher, Settings.ExchangeName, routingKey, Settings.Serializer, Settings.TypeProvider);

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

        private static void PublishJob(
            JobInfo job,
            IMesagePublisher publisher, 
            string exchangeName, 
            string routingKey,
            IJobSerializer serializer,
            IJobTypeProvider typeProvider) 
        {
            var serializedJob = serializer.Serialize(job, typeProvider);

            publisher.Publish(
                exchangeName,
                routingKey,
                serializedJob,
                typeProvider.TypeToTag(job.ArgsType),
                true
            );
        }

        protected void EnsurePublishersExist()
        {
            if (MessagePublishers.Count > 0)
            {
                var publishersOk = true;

                foreach (var publisher in MessagePublishers)
                {
                    if (!publisher.IsOk)
                    {
                        publishersOk = false;
                        break;
                    }
                }

                if (publishersOk) return;

                DisposePublishers();
            }
            
            try
            {
                MessageBroker = Settings.MessageBrokerFactory.CreateMessageBroker();
                MessageBroker.DeclareTopology();

                OnMessageBrokerCreated();

                var publishersCount = Settings.GetPublishersCount();

                for (var i = 0; i < publishersCount; i++)
                {
                    MessagePublishers.Add(MessageBroker.CreatePublisher());
                }
            }
            catch
            {
                DisposePublishers();
                throw;
            }
        }

        protected void DisposePublishers()
        {
            foreach (var publisher in MessagePublishers)
            {
                publisher.SafeDispose();
            }

            MessagePublishers.Clear();
            MessageBroker.SafeDispose();
            MessageBroker = null;
        }

        protected virtual void OnMessageBrokerCreated()
        {
        }
    }
}
