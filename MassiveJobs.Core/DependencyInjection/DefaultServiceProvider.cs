using System;
using MassiveJobs.Core.LightInject;
using MassiveJobs.Core.Serialization;

namespace MassiveJobs.Core.DependencyInjection
{
    public class DefaultServiceProvider : IJobServiceProvider, IJobServiceFactory, IJobServiceCollection
    {
        internal readonly IServiceContainer Container;

        public IJobServiceFactory ServiceFactory => this;
        public IJobServiceCollection ServiceCollection => this;

        public DefaultServiceProvider()
        {
            Container = new ServiceContainer();

            Container.RegisterSingleton<IJobRunner, DefaultJobRunner>();
            Container.RegisterSingleton<IJobServiceScopeFactory>(f => new DefaultServiceScopeFactory(f));
            Container.RegisterSingleton<IJobServiceFactory>(f => new DefaultServiceFactory(f));
            Container.RegisterSingleton(typeof(IJobLogger<>), typeof(DefaultJobLogger<>));

            Container.RegisterScoped<IJobPublisher, DefaultJobPublisher>();
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

        public void RegisterInstance<TService>(TService instance)
        {
            Container.RegisterInstance(instance);
        }

        public void RegisterSingleton<TService>(Func<IJobServiceFactory, TService> factory)
        {
            Container.RegisterSingleton(f => factory(f.GetInstance<IJobServiceFactory>()));
        }

        public void RegisterSingleton<TService, TImplementation>() where TImplementation : TService
        {
            Container.RegisterSingleton<TService, TImplementation>();
        }

        public void RegisterScoped<TService>(Func<IJobServiceFactory, TService> factory)
        {
            Container.RegisterScoped(f => factory(f.GetInstance<IJobServiceFactory>()));
        }

        public void RegisterScoped<TService, TImplementation>() where TImplementation : TService
        {
            Container.RegisterScoped<TService, TImplementation>();
        }

        public void RegisterTransient<TService>(Func<IJobServiceFactory, TService> factory)
        {
            Container.RegisterTransient(f => factory(f.GetInstance<IJobServiceFactory>()));
        }

        public void RegisterTransient<TService, TImplementation>() where TImplementation : TService
        {
            Container.RegisterTransient<TService, TImplementation>();
        }

        public void Validate()
        {
            var hasSerializer = false;
            var hasTypeProvider = false;
            var hasMessagePublisher = false;
            var hasMessageConsumer = false;
            var hasLoggerFactory = false;
            var hasSettings = false;

            foreach (var sr in Container.AvailableServices)
            {
                if (sr.ServiceType == typeof(MassiveJobsSettings)) hasSettings = true;
                else if (sr.ServiceType == typeof(IJobSerializer)) hasSerializer = true;
                else if (sr.ServiceType == typeof(IJobTypeProvider)) hasTypeProvider = true;
                else if (sr.ServiceType == typeof(IMessagePublisher)) hasMessagePublisher = true;
                else if (sr.ServiceType == typeof(IMessageConsumer)) hasMessageConsumer = true;
                else if (sr.ServiceType == typeof(IJobLoggerFactory)) hasLoggerFactory = true;
            }

            if (!hasMessagePublisher) throw new ArgumentNullException($"{nameof(IMessagePublisher)} must be registered");
            if (!hasMessageConsumer) throw new ArgumentNullException($"{nameof(IMessageConsumer)} must be registered");

            if (!hasSettings) RegisterInstance(new MassiveJobsSettings());
            if (!hasTypeProvider) RegisterSingleton<IJobTypeProvider, DefaultTypeProvider>();
            if (!hasSerializer) RegisterSingleton<IJobSerializer, DefaultSerializer>();
            if (!hasLoggerFactory) RegisterSingleton<IJobLoggerFactory, DefaultLoggerFactory>();
        }

        public void Compile()
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