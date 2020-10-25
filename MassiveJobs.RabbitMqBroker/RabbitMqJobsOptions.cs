
using MassiveJobs.Core;

namespace MassiveJobs.RabbitMqBroker
{
    public class RabbitMqJobsOptions
    {
        public int MaxDegreeOfParallelismPerWorker { get; set; } = 2;

        public int ImmediateWorkersCount { get; set; } = 2;
        public int ScheduledWorkersCount { get; set; } = 1;
        public int PeriodicWorkersCount { get; set; } = 1;

        public ushort PublishBatchSize { get; set; } = 100;

        public ushort ImmediateWorkersBatchSize { get; set; } = 100;
        public ushort ScheduledWorkersBatchSize { get; set; } = 100;
        public ushort PeriodicWorkersBatchSize { get; set; } = 100;

        public int MaxQueueLength { get; set; } = QueueLength.Default;

        public RabbitMqSettings RabbitMqSettings { get; set; }

        public IJobSerializer JobSerializer { get; set; }
        public IJobTypeProvider JobTypeProvider { get; set; }
        public IJobLoggerFactory JobLoggerFactory { get; set; }
    }
}
