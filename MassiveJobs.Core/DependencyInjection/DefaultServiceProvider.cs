using System;
using MassiveJobs.Core.LightInject;

namespace MassiveJobs.Core.DependencyInjection
{
    public class DefaultServiceProvider : IJobServiceProvider
    {
        internal readonly IServiceContainer Container;

        public DefaultServiceProvider(MassiveJobsSettings settings)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));

            Container = new ServiceContainer();

            Container.RegisterInstance(settings);

            Container.Register<IJobRunner, DefaultJobRunner>(new PerContainerLifetime());
            Container.Register<IJobServiceScopeFactory>(f => new DefaultServiceScopeFactory(f), new PerContainerLifetime());
            Container.Register<IJobPublisher, DefaultJobPublisher>(new PerScopeLifetime());
            Container.Register(typeof(IJobLogger<>), typeof(DefaultJobLogger<>));
        }

        public void Dispose()
        {
            Container.Dispose();
        }

        public object GetRequiredService(Type serviceType)
        {
            return Container.GetInstance(serviceType);
        }

        public object GetService(Type serviceType)
        {
            return Container.TryGetInstance(serviceType);
        }

        internal void Compile()
        {
            Container.Compile();
            Container.Compile<IJobLogger<WorkerCoordinator>>();
            Container.Compile<IJobLogger<WorkerImmediate>>();
            Container.Compile<IJobLogger<WorkerScheduled>>();
            Container.Compile<IJobLogger<DefaultJobPublisher>>();
            Container.Compile<IJobLogger<DefaultJobRunner>>();
        }
    }
}