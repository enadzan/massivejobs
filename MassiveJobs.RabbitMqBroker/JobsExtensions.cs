using System;
using Microsoft.Extensions.DependencyInjection;
using MassiveJobs.Core;

namespace MassiveJobs.RabbitMqBroker
{
    public static class JobsExtensions
    {
        public static JobsBuilder WithRabbitMqBroker(this JobsBuilder builder, Action<RabbitMqSettings> configureAction = null)
        {
            var rabbitMqSettings = new RabbitMqSettings();

            configureAction?.Invoke(rabbitMqSettings);

            builder.ServiceCollection.AddSingleton(rabbitMqSettings);

            builder.ServiceCollection.AddSingleton<IMessagePublisher, RabbitMqMessagePublisher>();
            builder.ServiceCollection.AddSingleton<IMessageConsumer, RabbitMqMessageConsumer>();

            return builder;
        }
    }
}
