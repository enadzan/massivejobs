using System;
using MassiveJobs.Core;

namespace MassiveJobs.RabbitMqBroker
{
    public static class JobsExtensions
    {
        public static JobsBuilder WithRabbitMqBroker(this JobsBuilder builder, Action<RabbitMqSettings> configureAction = null)
        {
            var rabbitMqSettings = new RabbitMqSettings();

            configureAction?.Invoke(rabbitMqSettings);

            builder.RegisterInstance(rabbitMqSettings);

            builder.RegisterSingleton<IMessagePublisher, RabbitMqMessagePublisher>();
            builder.RegisterSingleton<IMessageConsumer, RabbitMqMessageConsumer>();

            return builder;
        }
    }
}
