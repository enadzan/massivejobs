using System;
using MassiveJobs.Core;

namespace MassiveJobs.RabbitMqBroker
{
    public class RabbitMqJobsBroker
    {
        public static void Initialize(
            bool startWorkers = true,
            RabbitMqSettings rabbitMqSettings = null, 
            Action<MassiveJobsSettings> configureAction = null)
        {
            if (rabbitMqSettings == null) rabbitMqSettings = new RabbitMqSettings();

            var massiveJobsSettings = new MassiveJobsSettings
            {
                ImmediateQueueNameTemplate = $"{rabbitMqSettings.NamePrefix}{Constants.ImmediateQueueNameTemplate}",
                ScheduledQueueNameTemplate = $"{rabbitMqSettings.NamePrefix}{Constants.ScheduledQueueNameTemplate}",
                ErrorQueueName = $"{rabbitMqSettings.NamePrefix}{Constants.ErrorQueueName}",
                FailedQueueName = $"{rabbitMqSettings.NamePrefix}{Constants.FailedQueueName}",
                StatsQueueName = $"{rabbitMqSettings.NamePrefix}{Constants.StatsQueueName}",
                PeriodicQueueNameTemplate = $"{rabbitMqSettings.NamePrefix}{Constants.PeriodicQueueNameTemplate}"
            };

            configureAction?.Invoke(massiveJobsSettings);

            var publisher = new RabbitMqMessagePublisher(rabbitMqSettings, massiveJobsSettings);
            var consumer = new RabbitMqMessageConsumer(rabbitMqSettings, massiveJobsSettings);

            var scopeFactory = massiveJobsSettings.ServiceScopeFactory ?? new DefaultServiceScopeFactory(massiveJobsSettings);

            scopeFactory.ServiceCollection.AddSingleton<IWorkerCoordinator>(
                new WorkerCoordinator(massiveJobsSettings, consumer, scopeFactory, massiveJobsSettings.LoggerFactory)
            );
            scopeFactory.ServiceCollection.AddSingleton<IMessagePublisher>(publisher);
            scopeFactory.ServiceCollection.AddSingleton<IMessageConsumer>(consumer);

            var mediator = new MassiveJobsMediator(scopeFactory, massiveJobsSettings.LoggerFactory.SafeCreateLogger<MassiveJobsMediator>());

            MassiveJobsMediator.Initialize(mediator, startWorkers);
        }
    }
}
