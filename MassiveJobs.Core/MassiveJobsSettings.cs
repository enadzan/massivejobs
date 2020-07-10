namespace MassiveJobs.Core
{
    public class MassiveJobsSettings
    {
        public int ImmediateWorkersCount { get; set; } = 2;
        public int ScheduledWorkersCount { get; set; } = 1;
        public int PeriodicWorkersCount { get; set; } = 1;

        public ushort PublishBatchSize { get; set; } = 100;

        public ushort ImmediateWorkersBatchSize { get; set; } = 100;
        public ushort ScheduledWorkersBatchSize { get; set; } = 100;
        public ushort PeriodicWorkersBatchSize { get; set; } = 100;

        public string ImmediateQueueNameTemplate { get; set; } = "massivejobs.immediate_{0:#00}";
        public string ScheduledQueueNameTemplate { get; set; } = "massivejobs.scheduled_{0:#00}";
        public string PeriodicQueueNameTemplate { get; set; } = "massivejobs.periodic_{0:#00}";
        public string ErrorQueueName { get; set; } = "massivejobs.error";
        public string FailedQueueName { get; set; } = "massivejobs.failed";
        public string StatsQueueName { get; set; } = "massivejobs.stats";

        public int MaxQueueLength { get; set; } = QueueLength.Default;
    }

    public static class QueueLength
    {
        public const int NoLimit = -1;
        public const int Default = 50_000;
    }
}
