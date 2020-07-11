using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using MassiveJobs.Core;
using MassiveJobs.Core.Hosting;

namespace MassiveJobs.RabbitMqBroker.Hosting
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddMassiveJobs(
            this IServiceCollection serviceCollection, 
            Action<RabbitMqJobsOptions> configureAction = null
            )
        {
            var rabbitMqSettings = new RabbitMqSettings();

            var options = new RabbitMqJobsOptions
            {
                MassiveJobs = RabbitMqJobs.CreateJobsSettings(rabbitMqSettings)
            };

            configureAction?.Invoke(options);

            serviceCollection.AddSingleton(options.MassiveJobs);

            if (options.JobLoggerFactory != null)
            {
                serviceCollection.AddSingleton(options.JobLoggerFactory);
            }
            else
            {
                serviceCollection.AddSingleton<IJobLoggerFactory>(p => new LoggerFactoryWrapper(p.GetRequiredService<ILoggerFactory>()));
            }

            if (options.JobSerializer != null)
            {
                serviceCollection.AddSingleton(options.JobSerializer);
            }
            else
            {
                serviceCollection.AddSingleton<IJobSerializer, DefaultSerializer>();
            }

            if (options.JobTypeProvider != null)
            {
                serviceCollection.AddSingleton(options.JobTypeProvider);
            }
            else
            {
                serviceCollection.AddSingleton<IJobTypeProvider, DefaultTypeProvider>();
            }

            serviceCollection.AddSingleton<IJobRunner>(p => 
                new DefaultJobRunner(p.GetRequiredService<IJobLoggerFactory>().CreateLogger<DefaultJobRunner>()));

            serviceCollection.AddSingleton<IMessagePublisher>(p =>
                new RabbitMqMessagePublisher(rabbitMqSettings, options.MassiveJobs, p.GetRequiredService<IJobLoggerFactory>().CreateLogger<RabbitMqMessagePublisher>()));

            serviceCollection.AddSingleton<IMessageConsumer>(p => 
                new RabbitMqMessageConsumer(rabbitMqSettings, options.MassiveJobs, p.GetRequiredService<IJobLoggerFactory>().CreateLogger<RabbitMqMessageConsumer>()));

            serviceCollection.AddScoped<IJobPublisher>(svcProvider =>
            {
                return new DefaultJobPublisher(
                    svcProvider.GetService<MassiveJobsSettings>(),
                    svcProvider.GetService<IMessagePublisher>(),
                    svcProvider.GetService<IJobTypeProvider>(),
                    svcProvider.GetService<IJobSerializer>(),
                    svcProvider.GetService<IJobLoggerFactory>().CreateLogger<DefaultJobPublisher>()
                );
            });

            serviceCollection.AddHostedService<MassiveJobsBackgroundService>();

            return serviceCollection;
        }
    }
}
