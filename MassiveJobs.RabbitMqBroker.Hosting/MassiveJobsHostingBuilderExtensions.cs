using System;
using Microsoft.Extensions.DependencyInjection;

using MassiveJobs.Core;
using MassiveJobs.Core.Hosting;

namespace MassiveJobs.RabbitMqBroker.Hosting
{
    public static class MassiveJobsHostingBuilderExtensions
    {
        public static void UseRabbitMqBroker(this MassiveJobsHostingBuilder hostingBuilder, Action<RabbitMqSettings> configureAction)
        {
            var rabbitMqSettings = new RabbitMqSettings();

            configureAction?.Invoke(rabbitMqSettings);

            hostingBuilder.ServiceCollection.AddSingleton(rabbitMqSettings);
            hostingBuilder.ServiceCollection.AddSingleton<IMessagePublisher, RabbitMqMessagePublisher>();
            hostingBuilder.ServiceCollection.AddSingleton<IMessageConsumer, RabbitMqMessageConsumer>();
        }
    }
}