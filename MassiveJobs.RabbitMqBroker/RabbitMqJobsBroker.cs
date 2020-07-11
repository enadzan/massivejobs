using System;
using MassiveJobs.Core;

namespace MassiveJobs.RabbitMqBroker
{
    public class RabbitMqJobsBroker
    {
        public static void Initialize(
            bool startWorkers = true,
            RabbitMqSettings rabbitMqSettings = null, 
            Action<MassiveJobsSettings> configureAction = null,
            IJobLoggerFactory loggerFactory = null)
        {
            if (rabbitMqSettings == null) rabbitMqSettings = new RabbitMqSettings();

            var massiveJobsSettings = CreateMassiveJobsSettings(rabbitMqSettings);

            configureAction?.Invoke(massiveJobsSettings);

            var publisher = new RabbitMqMessagePublisher(rabbitMqSettings, massiveJobsSettings, loggerFactory.SafeCreateLogger<RabbitMqMessagePublisher>());
            var consumer = new RabbitMqMessageConsumer(rabbitMqSettings, massiveJobsSettings, loggerFactory.SafeCreateLogger<RabbitMqMessageConsumer>());

            var serviceScopeFactory = new DefaultServiceScopeFactory(massiveJobsSettings, publisher, consumer, loggerFactory);

            MassiveJobsMediator.Initialize(serviceScopeFactory);

            if (startWorkers)
            {
                MassiveJobsMediator.DefaultInstance.StartJobWorkers();
            }
        }

        private static MassiveJobsSettings CreateMassiveJobsSettings(RabbitMqSettings rabbitMqSettings)
        {
            return new MassiveJobsSettings
            {
                ImmediateQueueNameTemplate = $"{rabbitMqSettings.NamePrefix}{Constants.ImmediateQueueNameTemplate}",
                ScheduledQueueNameTemplate = $"{rabbitMqSettings.NamePrefix}{Constants.ScheduledQueueNameTemplate}",
                ErrorQueueName = $"{rabbitMqSettings.NamePrefix}{Constants.ErrorQueueName}",
                FailedQueueName = $"{rabbitMqSettings.NamePrefix}{Constants.FailedQueueName}",
                StatsQueueName = $"{rabbitMqSettings.NamePrefix}{Constants.StatsQueueName}",
                PeriodicQueueNameTemplate = $"{rabbitMqSettings.NamePrefix}{Constants.PeriodicQueueNameTemplate}"
            };
        }
    }
}
