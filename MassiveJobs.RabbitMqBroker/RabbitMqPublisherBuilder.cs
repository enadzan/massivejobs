using System;

using MassiveJobs.Core;

namespace MassiveJobs.RabbitMqBroker
{
    public class RabbitMqPublisherBuilder
    {
        private readonly RabbitMqSettings _rabbitMqSettings;
        private readonly MassiveJobsSettings _massiveJobsSettings;

        public static RabbitMqPublisherBuilder FromSettings(RabbitMqSettings settings)
        {
            return new RabbitMqPublisherBuilder(settings);
        }

        private RabbitMqPublisherBuilder(RabbitMqSettings settings)
        {
            _rabbitMqSettings = settings;

            _massiveJobsSettings = new MassiveJobsSettings
            {
                ImmediateQueueNameTemplate = $"{settings.NamePrefix}{Constants.ImmediateQueueNameTemplate}",
                ScheduledQueueNameTemplate = $"{settings.NamePrefix}{Constants.ScheduledQueueNameTemplate}",
                ErrorQueueName = $"{settings.NamePrefix}{Constants.ErrorQueueName}",
                FailedQueueName = $"{settings.NamePrefix}{Constants.FailedQueueName}",
                StatsQueueName = $"{settings.NamePrefix}{Constants.StatsQueueName}",
                PeriodicQueueName = $"{settings.NamePrefix}{Constants.PeriodicQueueName}"
            };
        }

        public RabbitMqPublisherBuilder ConfigurePublisher(Action<MassiveJobsSettings> configureAction)
        {
            configureAction?.Invoke(_massiveJobsSettings);
            return this;
        }

        public RabbitMqPublisherBuilder UsingLoggerFactory(ILoggerFactory loggerFactory)
        {
            _massiveJobsSettings.LoggerFactory = loggerFactory;
            return this;
        }

        public RabbitMqPublisherBuilder UsingServiceScopeFactory(IServiceScopeFactory serviceScopeFactory)
        {
            _massiveJobsSettings.ServiceScopeFactory = serviceScopeFactory;
            return this;
        }

        public IJobPublisher Build()
        {
            if (_massiveJobsSettings.ImmediateWorkersCount <= 0) throw new Exception($"{nameof(_massiveJobsSettings.ImmediateWorkersCount)} must be positive.");
            if (_massiveJobsSettings.ScheduledWorkersCount <= 0) throw new Exception($"{nameof(_massiveJobsSettings.ScheduledWorkersCount)} must be positive.");

            _massiveJobsSettings.MessageBrokerFactory = new RabbitMqMessageBrokerFactory(_rabbitMqSettings, _massiveJobsSettings);
            _massiveJobsSettings.JobRunner = new DefaultJobRunner(_massiveJobsSettings.LoggerFactory.SafeCreateLogger<DefaultJobRunner>());

            return new DefaultJobPublisher(_massiveJobsSettings);
        }
    }
}
