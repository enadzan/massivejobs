using System;
using MassiveJobs.Core.LightInject;

namespace MassiveJobs.Core.DependencyInjection
{
    public class DefaultJobServiceProvider : IJobServiceProvider
    {
        internal readonly IServiceContainer Container;

        public DefaultJobServiceProvider(MassiveJobsSettings settings, IJobLoggerFactory jobLoggerFactory = null)
        {
            Container = new ServiceContainer();

            Container.RegisterInstance(settings ?? new MassiveJobsSettings());
            Container.RegisterInstance(typeof(IJobLoggerFactory), jobLoggerFactory ?? new DefaultLoggerFactory());

            Container.Register<IJobRunner, DefaultJobRunner>(new PerContainerLifetime());
            Container.Register<IJobServiceScopeFactory>(f => new DefaultServiceScopeFactory(f), new PerContainerLifetime());
            Container.Register<IJobPublisher, DefaultJobPublisher>(new PerScopeLifetime());
            Container.Register(typeof(IJobLogger<>), typeof(DefaultLogger<>));
        }

        public void Dispose()
        {
            Container.Dispose();
        }

        public object GetRequiredService(Type serviceType)
        {
            var svc = GetService(serviceType);
            return svc ?? throw new ArgumentException($"Service of type {serviceType?.AssemblyQualifiedName} is not registered.");
        }

        public virtual object GetService(Type serviceType)
        {
            return Container.TryGetInstance(serviceType);
        }
    }
}