using System;

namespace MassiveJobs.Core.DependencyInjection
{
    public class DefaultServiceCollection : IJobServiceCollection
    {
        private readonly DefaultJobServiceProvider _serviceProvider;

        internal DefaultServiceCollection(DefaultJobServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        public void RegisterInstance<TService>(Func<IJobServiceProvider, TService> factory)
        {
            _serviceProvider.Container.RegisterInstance(factory(_serviceProvider));
        }

        public void Compile()
        {
            _serviceProvider.Container.Compile();
            _serviceProvider.Container.Compile<IJobLogger<WorkerCoordinator>>();
            _serviceProvider.Container.Compile<IJobLogger<WorkerImmediate>>();
            _serviceProvider.Container.Compile<IJobLogger<WorkerScheduled>>();
            _serviceProvider.Container.Compile<IJobLogger<DefaultJobPublisher>>();
            _serviceProvider.Container.Compile<IJobLogger<DefaultJobRunner>>();
        }
    }
}