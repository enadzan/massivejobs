namespace MassiveJobs.RabbitMqBroker
{
    static class Constants
    {
        public const string ExchangeName = "massivejobs.direct";

        public const string ImmediateQueueNameTemplate = "massivejobs.worker.{0:#00}";
        public const string ScheduledQueueNameTemplate = "massivejobs.scheduled.{0:#00}";
        public const string UnknownQueueName = "massivejobs.unknown";
        public const string ErrorQueueName = "massivejobs.error";
        public const string FailedQueueName = "massivejobs.failed";
        public const string StatsQueueName = "massivejobs.stats";

        public const int DefaultQueueLength = 100_000;
        public const int DefaultPublishTimeoutMs = 5 * 1000;
        public const int DefaultImmediateWorkersCount = 5;
        public const int DefaultScheduledWorkersCount = 2;

        public const ushort DefaultPublishBatchSize = 100;
        public const ushort DefaultConsumePrefetchCount = 1000;
        public const ushort DefaultConsumeBatchSize = 1000;

        public const int MaxTries = 25;
    }
}
