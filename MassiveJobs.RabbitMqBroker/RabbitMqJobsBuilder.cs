using System;
using MassiveJobs.Core;

namespace MassiveJobs.RabbitMqBroker
{
    public class RabbitMqJobsBuilder
    {
        private readonly RabbitMqSettings _rabbitMqSettings;
        private readonly MassiveJobsSettings _massiveJobsSettings;

        public static RabbitMqJobsBuilder FromSettings(RabbitMqSettings settings)
        {
            return new RabbitMqJobsBuilder(settings);
        }

        private RabbitMqJobsBuilder(RabbitMqSettings settings)
        {
            _rabbitMqSettings = settings;

            _massiveJobsSettings = new MassiveJobsSettings
            {
                ImmediateQueueNameTemplate = $"{settings.NamePrefix}{Constants.ImmediateQueueNameTemplate}",
                ScheduledQueueNameTemplate = $"{settings.NamePrefix}{Constants.ScheduledQueueNameTemplate}",
                ErrorQueueName = $"{settings.NamePrefix}{Constants.ErrorQueueName}",
                FailedQueueName = $"{settings.NamePrefix}{Constants.FailedQueueName}",
                StatsQueueName = $"{settings.NamePrefix}{Constants.StatsQueueName}",
                PeriodicQueueNameTemplate = $"{settings.NamePrefix}{Constants.PeriodicQueueNameTemplate}"
            };
        }

        public RabbitMqJobsBuilder Configure(Action<MassiveJobsSettings> configureAction)
        {
            configureAction?.Invoke(_massiveJobsSettings);
            return this;
        }

        public Jobs Build()
        {
            var publisher = new RabbitMqMessagePublisher(_rabbitMqSettings, _massiveJobsSettings);
            var consumer = new RabbitMqMessageConsumer(_rabbitMqSettings, _massiveJobsSettings);

            var scopeFactory = _massiveJobsSettings.ServiceScopeFactory ?? new DefaultServiceScopeFactory(_massiveJobsSettings);

            scopeFactory.ServiceCollection.AddSingleton<IWorkerCoordinator>(
                new WorkerCoordinator(_massiveJobsSettings, consumer, scopeFactory, _massiveJobsSettings.LoggerFactory)
            );
            scopeFactory.ServiceCollection.AddSingleton<IMessagePublisher>(publisher);
            scopeFactory.ServiceCollection.AddSingleton<IMessageConsumer>(consumer);

            return new Jobs(scopeFactory, _massiveJobsSettings.LoggerFactory.SafeCreateLogger<Jobs>());
        }
    }
}
