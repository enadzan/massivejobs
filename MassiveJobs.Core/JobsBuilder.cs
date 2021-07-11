using System;
using MassiveJobs.Core.DependencyInjection;

namespace MassiveJobs.Core
{
    public class JobsBuilder
    {
        private readonly IJobServiceProvider _serviceProvider;

        private JobsBuilder(IJobServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        }

        public static JobsBuilder Configure(IJobServiceProvider serviceProvider)
        {
            return new JobsBuilder(serviceProvider);
        }

        public static void DisposeJobs()
        {
            MassiveJobsMediator.Deinitialize();
        }

        public static JobsBuilder Configure()
        {
            return new JobsBuilder(new DefaultServiceProvider());
        }

        public JobsBuilder WithSettings(string namePrefix = "", Action<MassiveJobsSettings> configureAction = null)
        {
            var settings = new MassiveJobsSettings(namePrefix);
            configureAction?.Invoke(settings);
            RegisterInstance(settings);
            return this;
        }

        public JobsBuilder RegisterInstance<TService>(TService instance)
        {
            _serviceProvider.ServiceCollection.RegisterInstance(instance);
            return this;
        }

        public JobsBuilder RegisterSingleton<TService>(Func<IJobServiceFactory, TService> factory)
        {
            _serviceProvider.ServiceCollection.RegisterSingleton(factory);
            return this;
        }

        public JobsBuilder RegisterSingleton<TService, TImplementation>() where TImplementation : TService
        {
            _serviceProvider.ServiceCollection.RegisterSingleton<TService, TImplementation>();
            return this;
        }

        public JobsBuilder RegisterScoped<TService>(Func<IJobServiceFactory, TService> factory)
        {
            _serviceProvider.ServiceCollection.RegisterScoped(factory);
            return this;
        }

        public JobsBuilder RegisterScoped<TService, TImplementation>() where TImplementation : TService
        {
            _serviceProvider.ServiceCollection.RegisterScoped<TService, TImplementation>();
            return this;
        }

        public JobsBuilder RegisterTransient<TService>(Func<IJobServiceFactory, TService> factory)
        {
            _serviceProvider.ServiceCollection.RegisterTransient(factory);
            return this;
        }

        public JobsBuilder RegisterTransient<TService, TImplementation>() where TImplementation : TService
        {
            _serviceProvider.ServiceCollection.RegisterTransient<TService, TImplementation>();
            return this;
        }

        public void Build(bool startWorkers = true)
        {
            ValidateAndCompile();
            MassiveJobsMediator.Initialize(_serviceProvider.ServiceFactory);

            if (startWorkers)
            {
                MassiveJobsMediator.DefaultInstance.StartJobWorkers();
            }
        }

        private void ValidateAndCompile()
        {
            _serviceProvider.ServiceCollection.Validate();
            _serviceProvider.ServiceCollection.Compile();
        }
    }
}
