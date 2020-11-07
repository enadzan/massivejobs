namespace MassiveJobs.Core
{
    public class MassiveJobsSettings
    {
        public int MaxDegreeOfParallelismPerWorker { get; set; } = 2;

        public int ImmediateWorkersCount { get; set; } = 2;
        public int ScheduledWorkersCount { get; set; } = 1;

        public int LongRunningImmediateWorkersCount { get; set; } = 1;
        public int LongRunningScheduledWorkersCount { get; set; } = 1;

        public ushort PublishBatchSize { get; set; } = 100;

        public ushort ImmediateWorkersBatchSize { get; set; } = 100;
        public ushort ScheduledWorkersBatchSize { get; set; } = 100;

        public string ImmediateQueueNameTemplate { get; set; } = "massivejobs.immediate.{0:#00}";
        public string ScheduledQueueNameTemplate { get; set; } = "massivejobs.scheduled.{0:#00}";

        public string LongRunningImmediateQueueNameTemplate { get; set; } = "massivejobs.long.immediate.{0:#00}";
        public string LongRunningScheduledQueueNameTemplate { get; set; } = "massivejobs.long.scheduled.{0:#00}";

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
