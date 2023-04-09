using System;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

using MassiveJobs.Core.Serialization;

namespace MassiveJobs.Core
{
    public class JobsBuilder
    {
        public IServiceCollection ServiceCollection { get; }

        private JobsBuilder(IServiceCollection serviceCollection)
        {
            ServiceCollection = serviceCollection;
        }

        public static JobsBuilder Configure(IServiceCollection serviceCollection)
        {
            return new JobsBuilder(serviceCollection);
        }

        public static void DisposeJobs()
        {
            MassiveJobsMediator.Deinitialize();
        }

        public JobsBuilder WithDefaultImplementations(MassiveJobsSettings settings)
        {
            ServiceCollection.AddSingleton(settings);

            ServiceCollection.TryAddSingleton<IWorkerCoordinator, WorkerCoordinator>();
            ServiceCollection.TryAddSingleton<IJobRunner, DefaultJobRunner>();
            ServiceCollection.TryAddSingleton<IJobSerializer, DefaultSerializer>();
            ServiceCollection.TryAddSingleton<IJobTypeProvider, DefaultTypeProvider>();

            ServiceCollection.TryAddScoped<IJobPublisher, DefaultJobPublisher>();

            return this;
        }

        public void Build(IServiceProvider serviceProvider, bool startWorkers = true)
        {
            MassiveJobsMediator.Initialize(serviceProvider);

            if (startWorkers)
            {
                MassiveJobsMediator.DefaultInstance.StartJobWorkers();
            }
        }
    }
}
