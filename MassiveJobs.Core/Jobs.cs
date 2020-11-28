using System;
using MassiveJobs.Core.LightInject;
using MassiveJobs.Core.DependencyInjection;
using MassiveJobs.Core.Serialization;

namespace MassiveJobs.Core
{
    public class Jobs
    {
        private readonly DefaultServiceProvider _serviceProvider;

        private Jobs(DefaultServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        public static Jobs Configure(MassiveJobsSettings settings = null)
        {
            return new Jobs(new DefaultServiceProvider(settings ?? new MassiveJobsSettings()));
        }

        public static void Deinitialize()
        {
            MassiveJobsMediator.Deinitialize();
        }

        public Jobs RegisterInstance<TService>(TService instance)
        {
            _serviceProvider.Container.RegisterInstance(instance);
            return this;
        }

        public Jobs RegisterSingleton<TService>(Func<IJobServiceFactory, TService> factory)
        {
            _serviceProvider.Container.RegisterSingleton(f => factory(new DefaultServiceFactory(f)));
            return this;
        }

        public Jobs RegisterSingleton<TService, TImplementation>() where TImplementation : TService
        {
            _serviceProvider.Container.RegisterSingleton<TService, TImplementation>();
            return this;
        }

        public Jobs RegisterScoped<TService>(Func<IJobServiceFactory, TService> factory)
        {
            _serviceProvider.Container.RegisterScoped(f => factory(new DefaultServiceFactory(f)));
            return this;
        }

        public Jobs RegisterScoped<TService, TImplementation>() where TImplementation : TService
        {
            _serviceProvider.Container.RegisterScoped<TService, TImplementation>();
            return this;
        }

        public Jobs RegisterTransient<TService>(Func<IJobServiceFactory, TService> factory)
        {
            _serviceProvider.Container.RegisterTransient(f => factory(new DefaultServiceFactory(f)));
            return this;
        }

        public Jobs RegisterTransient<TService, TImplementation>() where TImplementation : TService
        {
            _serviceProvider.Container.RegisterTransient<TService, TImplementation>();
            return this;
        }

        public void Initialize(bool startWorkers = true)
        {
            Validate();
            MassiveJobsMediator.Initialize(_serviceProvider);

            if (startWorkers)
            {
                MassiveJobsMediator.DefaultInstance.StartJobWorkers();
            }
        }

        internal MassiveJobsMediator InitializeNew()
        {
            Validate();
            return new MassiveJobsMediator(_serviceProvider);
        }

        private void Validate()
        {
            var hasSerializer = false;
            var hasTypeProvider = false;
            var hasMessagePublisher = false;
            var hasMessageConsumer = false;
            var hasLoggerFactory = false;

            foreach (var sr in _serviceProvider.Container.AvailableServices)
            {
                if (sr.ServiceType == typeof(IJobSerializer)) hasSerializer = true;
                else if (sr.ServiceType == typeof(IJobTypeProvider)) hasTypeProvider = true;
                else if (sr.ServiceType == typeof(IMessagePublisher)) hasMessagePublisher = true;
                else if (sr.ServiceType == typeof(IMessageConsumer)) hasMessageConsumer = true;
                else if (sr.ServiceType == typeof(IJobLoggerFactory)) hasLoggerFactory = true;
            }

            if (!hasMessagePublisher) throw new ArgumentNullException($"{nameof(IMessagePublisher)} must be registered");
            if (!hasMessageConsumer) throw new ArgumentNullException($"{nameof(IMessageConsumer)} must be registered");

            if (!hasTypeProvider) RegisterSingleton<IJobTypeProvider, DefaultTypeProvider>();
            if (!hasSerializer) RegisterSingleton<IJobSerializer, DefaultSerializer>();
            if (!hasLoggerFactory) RegisterSingleton<IJobLoggerFactory, DefaultLoggerFactory>();

            _serviceProvider.Compile();
        }
    }
}
