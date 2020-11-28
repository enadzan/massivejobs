using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

using MassiveJobs.Core.Serialization;
using MassiveJobs.Core.DependencyInjection;

namespace MassiveJobs.Core.Hosting
{
    public static class ServiceCollectionExtensions
    {
        public static MassiveJobsHostingBuilder AddMassiveJobs(
            this IServiceCollection serviceCollection,
            Action<MassiveJobsHostingOptions> configureAction = null)
        {
            var options = new MassiveJobsHostingOptions();
            configureAction?.Invoke(options);

            serviceCollection.AddSingleton(options);
            serviceCollection.AddSingleton(options.MassiveJobsSettings);

            serviceCollection.AddSingleton(typeof(IJobLogger<>), typeof(LoggerWrapper<>));

            serviceCollection.TryAddSingleton<IJobRunner, DefaultJobRunner>();
            serviceCollection.TryAddSingleton<IJobSerializer, DefaultSerializer>();
            serviceCollection.TryAddSingleton<IJobTypeProvider, DefaultTypeProvider>();
            serviceCollection.TryAddSingleton<IJobServiceProvider, ServiceProviderWrapper>();
            serviceCollection.TryAddSingleton<IJobServiceScopeFactory, ServiceScopeFactoryWrapper>();

            serviceCollection.AddScoped<IJobPublisher, DefaultJobPublisher>();

            serviceCollection.AddHostedService<MassiveJobsBackgroundService>();

            return new MassiveJobsHostingBuilder(serviceCollection);
        }
    }
}
