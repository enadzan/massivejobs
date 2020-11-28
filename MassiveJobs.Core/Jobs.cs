using System;
using MassiveJobs.Core.DependencyInjection;

namespace MassiveJobs.Core
{
    public class Jobs
    {
        private readonly IJobServiceProvider _serviceProvider;

        private Jobs(IJobServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        }

        public static Jobs Configure(IJobServiceProvider serviceProvider)
        {
            return new Jobs(serviceProvider);
        }

        public static Jobs Configure()
        {
            return new Jobs(new DefaultServiceProvider());
        }

        public static void Deinitialize()
        {
            MassiveJobsMediator.Deinitialize();
        }

        public Jobs RegisterInstance<TService>(TService instance)
        {
            _serviceProvider.ServiceCollection.RegisterInstance(instance);
            return this;
        }

        public Jobs RegisterSingleton<TService>(Func<IJobServiceFactory, TService> factory)
        {
            _serviceProvider.ServiceCollection.RegisterSingleton(factory);
            return this;
        }

        public Jobs RegisterSingleton<TService, TImplementation>() where TImplementation : TService
        {
            _serviceProvider.ServiceCollection.RegisterSingleton<TService, TImplementation>();
            return this;
        }

        public Jobs RegisterScoped<TService>(Func<IJobServiceFactory, TService> factory)
        {
            _serviceProvider.ServiceCollection.RegisterScoped(factory);
            return this;
        }

        public Jobs RegisterScoped<TService, TImplementation>() where TImplementation : TService
        {
            _serviceProvider.ServiceCollection.RegisterScoped<TService, TImplementation>();
            return this;
        }

        public Jobs RegisterTransient<TService>(Func<IJobServiceFactory, TService> factory)
        {
            _serviceProvider.ServiceCollection.RegisterTransient(factory);
            return this;
        }

        public Jobs RegisterTransient<TService, TImplementation>() where TImplementation : TService
        {
            _serviceProvider.ServiceCollection.RegisterTransient<TService, TImplementation>();
            return this;
        }

        public void Initialize(bool startWorkers = true)
        {
            ValidateAndCompile();
            MassiveJobsMediator.Initialize(_serviceProvider);

            if (startWorkers)
            {
                MassiveJobsMediator.DefaultInstance.StartJobWorkers();
            }
        }

        internal MassiveJobsMediator InitializeNew()
        {
            ValidateAndCompile();
            return new MassiveJobsMediator(_serviceProvider);
        }

        private void ValidateAndCompile()
        {
            _serviceProvider.ServiceCollection.Validate();
            _serviceProvider.ServiceCollection.Compile();
        }
    }
}
