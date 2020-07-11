using System;
using MassiveJobs.Core;

namespace MassiveJobs.RabbitMqBroker
{
    public class RabbitMqJobs
    {
        public static void Initialize(bool startWorkers = true, RabbitMqSettings rabbitMqSettings = null, Action<RabbitMqJobsOptions> configureAction = null)
        {
            rabbitMqSettings = rabbitMqSettings ?? new RabbitMqSettings();

            var options = new RabbitMqJobsOptions
            {
                MassiveJobs = CreateJobsSettings(rabbitMqSettings)
            };

            configureAction?.Invoke(options);

            var publisher = new RabbitMqMessagePublisher(rabbitMqSettings, options.MassiveJobs, options.JobLoggerFactory.SafeCreateLogger<RabbitMqMessagePublisher>());
            var consumer = new RabbitMqMessageConsumer(rabbitMqSettings, options.MassiveJobs, options.JobLoggerFactory.SafeCreateLogger<RabbitMqMessageConsumer>());

            var serviceScopeFactory = new DefaultServiceScopeFactory(
                options.MassiveJobs,
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

        public static MassiveJobsSettings CreateJobsSettings(RabbitMqSettings rabbitMqSettings)
        {
            return new MassiveJobsSettings
            {
                ImmediateQueueNameTemplate = $"{rabbitMqSettings.NamePrefix}{Constants.ImmediateQueueNameTemplate}",
                ScheduledQueueNameTemplate = $"{rabbitMqSettings.NamePrefix}{Constants.ScheduledQueueNameTemplate}",
                ErrorQueueName = $"{rabbitMqSettings.NamePrefix}{Constants.ErrorQueueName}",
                FailedQueueName = $"{rabbitMqSettings.NamePrefix}{Constants.FailedQueueName}",
                StatsQueueName = $"{rabbitMqSettings.NamePrefix}{Constants.StatsQueueName}",
                PeriodicQueueNameTemplate = $"{rabbitMqSettings.NamePrefix}{Constants.PeriodicQueueNameTemplate}",
            };
        }
    }
}
