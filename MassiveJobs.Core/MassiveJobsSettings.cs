using System;

namespace MassiveJobs.Core
{
    public class MassiveJobsSettings
    {
        public IMessageBrokerFactory MessageBrokerFactory { get; set; }
        public IServiceScopeFactory ServiceScopeFactory { get; set; }
        public ILoggerFactory LoggerFactory { get; set; }

        public IJobRunner JobRunner { get; set; } = new DefaultJobRunner();
        public IJobSerializer Serializer = new DefaultSerializer();
        public IJobTypeProvider TypeProvider = new DefaultTypeProvider();

        public int ImmediateWorkersCount { get; set; } = 2;
        public int ScheduledWorkersCount { get; set; } = 2;

        public ushort PublishBatchSize { get; set; } = 100;
        public ushort ConsumeBatchSize { get; set; } = 1000;

        public string ImmediateQueueNameTemplate { get; set; }
        public string ScheduledQueueNameTemplate { get; set; }
        public string ErrorQueueName { get; set; }
        public string FailedQueueName { get; set; }
        public string StatsQueueName { get; set; }

        public int MaxQueueLength { get; set; } = QueueLength.Default;

        internal protected virtual int GetPublishersCount()
        {
            return Math.Max(ImmediateWorkersCount, ScheduledWorkersCount);
        }
    }

    public static class QueueLength
    {
        public const int NoLimit = -1;
        public const int Default = 50_000;
    }
}
