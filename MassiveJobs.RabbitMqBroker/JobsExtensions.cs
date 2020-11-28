using System;
using MassiveJobs.Core;

using MassiveJobs.Core.DependencyInjection;

namespace MassiveJobs.RabbitMqBroker
{
    public static class JobsExtensions
    {
        public static Jobs WithRabbitMqBroker(this Jobs jobs, Action<RabbitMqSettings> configureAction = null)
        {
            var rabbitMqSettings = new RabbitMqSettings();

            configureAction?.Invoke(rabbitMqSettings);

            jobs.ServiceCollection.RegisterInstance(_ => rabbitMqSettings);

            jobs.WithMessageBroker(
                p => new RabbitMqMessagePublisher(
                    p.GetRequiredService<RabbitMqSettings>(),
                    p.GetRequiredService<MassiveJobsSettings>(),
                    p.GetRequiredService<IJobLogger<RabbitMqMessagePublisher>>()),
                p => new RabbitMqMessageConsumer(
                    p.GetRequiredService<RabbitMqSettings>(),
                    p.GetRequiredService<MassiveJobsSettings>(),
                    p.GetRequiredService<IJobLogger<RabbitMqMessagePublisher>>())
            );

            return jobs;
        }
    }
}
