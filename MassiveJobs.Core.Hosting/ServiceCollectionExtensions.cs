using System;
using Microsoft.Extensions.DependencyInjection;

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

            options.MassiveJobsSettings = options.MassiveJobsSettings ?? new MassiveJobsSettings();

            JobsBuilder.Configure(serviceCollection)
                .WithDefaultImplementations(options.MassiveJobsSettings);

            serviceCollection.AddSingleton(options);
            serviceCollection.AddHostedService<MassiveJobsBackgroundService>();

            return new MassiveJobsHostingBuilder(serviceCollection);
        }
    }
}
