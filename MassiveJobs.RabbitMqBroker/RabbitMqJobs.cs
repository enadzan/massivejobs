using System;
using MassiveJobs.Core;

namespace MassiveJobs.RabbitMqBroker
{
    public class RabbitMqJobs
    {
        public static void Initialize(bool startWorkers = true, Action<RabbitMqJobsOptions> configureAction = null)
        {
            var options = new RabbitMqJobsOptions
            {
                RabbitMqSettings = new RabbitMqSettings()
            };

            configureAction?.Invoke(options);

            var massiveJobsSettings = CreateJobsSettings(options);

            var publisher = new RabbitMqMessagePublisher(options.RabbitMqSettings, massiveJobsSettings, options.JobLoggerFactory.SafeCreateLogger<RabbitMqMessagePublisher>());
            var consumer = new RabbitMqMessageConsumer(options.RabbitMqSettings, massiveJobsSettings, options.JobLoggerFactory.SafeCreateLogger<RabbitMqMessageConsumer>());

            var serviceScopeFactory = new DefaultServiceScopeFactory(
                massiveJobsSettings,
                publisher,
                consumer,
                options.JobLoggerFactory,
                options.JobSerializer,
                options.JobTypeProvider
            );

            MassiveJobsMediator.Initialize(serviceScopeFactory);

            if (startWorkers)
            {
                MassiveJobsMediator.DefaultInstance.StartJobWorkers();
            }
        }

        public static MassiveJobsSettings CreateJobsSettings(RabbitMqJobsOptions options)
        {
            var rabbitMqSettings = options.RabbitMqSettings;

            return new MassiveJobsSettings
            {
                MaxDegreeOfParallelismPerWorker = options.MaxDegreeOfParallelismPerWorker,

                ImmediateWorkersCount = options.ImmediateWorkersCount,
                LongRunningWorkersCount = options.LongRunningWorkersCount,
                ScheduledWorkersCount = options.ScheduledWorkersCount,
                PeriodicWorkersCount = options.PeriodicWorkersCount,
                PublishBatchSize = options.PublishBatchSize,

                ImmediateWorkersBatchSize = options.ImmediateWorkersBatchSize,
                LongRunningWorkersBatchSize = options.LongRunningWorkersBatchSize,
                ScheduledWorkersBatchSize = options.ScheduledWorkersBatchSize,
                PeriodicWorkersBatchSize = options.PeriodicWorkersBatchSize,

                MaxQueueLength = options.MaxQueueLength,

                ImmediateQueueNameTemplate = $"{rabbitMqSettings.NamePrefix}{Constants.ImmediateQueueNameTemplate}",
                LongRunningQueueNameTemplate = $"{rabbitMqSettings.NamePrefix}{Constants.LongRunningQueueNameTemplate}",
                ScheduledQueueNameTemplate = $"{rabbitMqSettings.NamePrefix}{Constants.ScheduledQueueNameTemplate}",
                ErrorQueueName = $"{rabbitMqSettings.NamePrefix}{Constants.ErrorQueueName}",
                FailedQueueName = $"{rabbitMqSettings.NamePrefix}{Constants.FailedQueueName}",
                StatsQueueName = $"{rabbitMqSettings.NamePrefix}{Constants.StatsQueueName}",
                PeriodicQueueNameTemplate = $"{rabbitMqSettings.NamePrefix}{Constants.PeriodicQueueNameTemplate}",
            };
        }
    }
}
