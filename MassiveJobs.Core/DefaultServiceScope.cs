﻿using System;
using MassiveJobs.Core.LightInject;
using MassiveJobs.Core.Serialization;

namespace MassiveJobs.Core
{
    public class DefaultJobServiceProvider : IJobServiceProvider
    {
        private readonly IServiceContainer _container;

        public DefaultJobServiceProvider(
            MassiveJobsSettings settings,
            IMessagePublisher messagePublisher,
            IMessageConsumer messageConsumer,
            IJobLoggerFactory jobLoggerFactory = null,
            IJobSerializer jobSerializer = null,
            IJobTypeProvider jobTypeProvider = null)
        {
            _container = new ServiceContainer();
            _container.RegisterInstance(settings);
            _container.RegisterInstance(typeof(IMessagePublisher), messagePublisher);
            _container.RegisterInstance(typeof(IMessageConsumer), messageConsumer);
            _container.RegisterInstance(typeof(IJobLoggerFactory), jobLoggerFactory ?? new DefaultLoggerFactory());
            _container.RegisterInstance(typeof(IJobSerializer), jobSerializer ?? new DefaultSerializer());
            _container.RegisterInstance(typeof(IJobTypeProvider), jobTypeProvider ?? new DefaultTypeProvider());
            _container.Register<IJobRunner, DefaultJobRunner>(new PerContainerLifetime());
            _container.Register<IJobServiceScopeFactory>(f => new DefaultJobServiceScopeFactory(f), new PerContainerLifetime());
            _container.Register<IJobPublisher, DefaultJobPublisher>(new PerScopeLifetime());
            _container.Register(typeof(IJobLogger<>), typeof(DefaultLogger<>));

            _container.Compile();
            _container.Compile<IJobLogger<WorkerCoordinator>>();
            _container.Compile<IJobLogger<WorkerImmediate>>();
            _container.Compile<IJobLogger<WorkerScheduled>>();
            _container.Compile<IJobLogger<DefaultJobPublisher>>();
            _container.Compile<IJobLogger<DefaultJobRunner>>();
        }

        public void Dispose()
        {
            _container.Dispose();
        }

        public object GetRequiredService(Type serviceType)
        {
            var svc = GetService(serviceType);
            return svc ?? throw new ArgumentException($"Service of type {serviceType?.AssemblyQualifiedName} is not registered.");
        }

        public virtual object GetService(Type serviceType)
        {
            return _container.TryGetInstance(serviceType);
        }
    }

    class DefaultJobServiceScopeFactory : IJobServiceScopeFactory
    {
        private readonly IServiceFactory _factory;

        public DefaultJobServiceScopeFactory(IServiceFactory factory)
        {
            _factory = factory;
        }

        public void Dispose()
        {
        }

        public IJobServiceScope CreateScope()
        {
            return new DefaultJobServiceScope(_factory.BeginScope());
        }
    }

    class DefaultJobServiceScope : IJobServiceScope
    {
        private readonly Scope _scope;

        internal DefaultJobServiceScope(Scope scope)
        {
            _scope = scope;
        }

        public object GetRequiredService(Type serviceType)
        {
            var svc = GetService(serviceType);
            return svc ?? throw new ArgumentException($"Service of type {serviceType?.AssemblyQualifiedName} is not registered.");
        }

        public virtual object GetService(Type serviceType)
        {
            return _scope.TryGetInstance(serviceType);
        }

        public void Dispose()
        {
            _scope.Dispose();
        }
    }

    public static class JobServiceProviderExtensions
    {
        public static TService GetService<TService>(this IJobServiceProvider provider)
        {
            return (TService)provider?.GetService(typeof(TService));
        }

        public static TService GetRequiredService<TService>(this IJobServiceProvider provider)
        {
            return (TService)provider?.GetRequiredService(typeof(TService));
        }
    }
}
