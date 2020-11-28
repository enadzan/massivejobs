namespace MassiveJobs.Core
{
    public class MassiveJobsSettings
    {
        public MassiveJobsSettings(string namePrefix = "")
        {
            NamePrefix = namePrefix;
            ImmediateQueueNameTemplate = namePrefix + "massivejobs.immediate.{0:#00}";
            LongRunningQueueNameTemplate = namePrefix + "massivejobs.long-running.{0:#00}";
            ScheduledQueueNameTemplate = namePrefix + "massivejobs.scheduled.{0:#00}";
            PeriodicQueueNameTemplate = namePrefix + "massivejobs.periodic.{0:#00}";
            ErrorQueueName = namePrefix + "massivejobs.error";
            FailedQueueName = namePrefix + "massivejobs.failed";
            StatsQueueName = namePrefix + "massivejobs.stats";
        }

        public int MaxDegreeOfParallelismPerWorker { get; set; } = 2;

        public int ImmediateWorkersCount { get; set; } = 2;
        public int LongRunningWorkersCount { get; set; } = 1;
        public int ScheduledWorkersCount { get; set; } = 1;
        public int PeriodicWorkersCount { get; set; } = 1;

        public ushort PublishBatchSize { get; set; } = 100;

        public ushort ImmediateWorkersBatchSize { get; set; } = 100;
        public ushort LongRunningWorkersBatchSize { get; set; } = 100;
        public ushort ScheduledWorkersBatchSize { get; set; } = 100;
        public ushort PeriodicWorkersBatchSize { get; set; } = 100;

        public string ImmediateQueueNameTemplate { get; }
        public string LongRunningQueueNameTemplate { get; }
        public string ScheduledQueueNameTemplate { get; }
        public string PeriodicQueueNameTemplate { get; }

        public string ErrorQueueName { get; }
        public string FailedQueueName { get; }
        public string StatsQueueName { get; }

        public int MaxQueueLength { get; set; } = QueueLength.Default;

        /// <summary>
        /// Prefix to be appended to all the queue names.
        /// </summary>
        public string NamePrefix { get; }
    }

    public static class QueueLength
    {
        public const int NoLimit = -1;
        public const int Default = 50_000;
    }
}
