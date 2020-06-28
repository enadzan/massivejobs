using System;
using MassiveJobs.Core;

namespace MassiveJobs.RabbitMqBroker
{
    public class RabbitMqBrokerBuilder
    {
        private readonly RabbitMqSettings _rabbitMqSettings;
        private readonly MassiveJobsSettings _massiveJobsSettings;

        public static RabbitMqBrokerBuilder FromSettings(RabbitMqSettings settings)
        {
            return new RabbitMqBrokerBuilder(settings);
        }

        private RabbitMqBrokerBuilder(RabbitMqSettings settings)
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

        public RabbitMqBrokerBuilder Configure(Action<MassiveJobsSettings> configureAction)
        {
            configureAction?.Invoke(_massiveJobsSettings);
            return this;
        }

        public IServiceScopeFactory GetScopeFactory()
        {
            var publisher = new RabbitMqMessagePublisher(_rabbitMqSettings, _massiveJobsSettings);
            var consumer = new RabbitMqMessageConsumer(_rabbitMqSettings, _massiveJobsSettings);

            if (_massiveJobsSettings.ServiceScopeFactory != null)
            {
                var scopeFactory = _massiveJobsSettings.ServiceScopeFactory;

                scopeFactory.ServiceCollection.AddSingleton<IMessagePublisher>(publisher);
                scopeFactory.ServiceCollection.AddSingleton<IMessageConsumer>(consumer);

                return scopeFactory;
            }
            else
            {
                var scopeFactory = new DefaultServiceScopeFactory(_massiveJobsSettings);

                scopeFactory.ServiceCollection.AddSingleton<IWorkerCoordinator>(
                    new WorkerCoordinator(_massiveJobsSettings, consumer, scopeFactory, _massiveJobsSettings.LoggerFactory)
                );
                scopeFactory.ServiceCollection.AddSingleton<IMessagePublisher>(publisher);
                scopeFactory.ServiceCollection.AddSingleton<IMessageConsumer>(consumer);
                
                return scopeFactory;
            }
        }

    }
}
