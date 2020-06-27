using System;

namespace MassiveJobs.Core
{
    class DefaultServiceScope : IServiceScopeFactory, IServiceScope
    {
        private readonly object _singletonLock = new object();

        private readonly MassiveJobsSettings _settings;

        private readonly IMessagePublisher _messagePublisher;
        private readonly IMessageConsumer _messageConsumer;
        private readonly IJobRunner _jobRunner;
        private readonly IJobSerializer _jobSerializer;
        private readonly IJobTypeProvider _jobTypeProvider;
        private IWorkerCoordinator _workerCoordinator;

        public DefaultServiceScope(MassiveJobsSettings settings, IMessagePublisher messagePublisher, IMessageConsumer messageConsumer)
        {
            _settings = settings;
            _messagePublisher = messagePublisher;
            _messageConsumer = messageConsumer;
            _jobRunner = new DefaultJobRunner(_settings.LoggerFactory.SafeCreateLogger<DefaultJobRunner>());

            _jobSerializer = settings.JobSerializer ?? new DefaultSerializer();
            _jobTypeProvider = settings.JobTypeProvider ?? new DefaultTypeProvider();
        }

        public IServiceScope CreateScope()
        {
            return this;
        }

        public object GetService(Type serviceType)
        {
            if (serviceType == typeof(IJobPublisher))
            {
                return new DefaultJobPublisher(
                    _settings,
                    _messagePublisher,
                    _jobTypeProvider,
                    _jobSerializer,
                    _settings.LoggerFactory.SafeCreateLogger<DefaultJobPublisher>()
                );
            }

            if (serviceType == typeof(IJobRunner))
            {
                return _jobRunner;
            }

            if (serviceType == typeof(MassiveJobsSettings))
            {
                return _settings;
            }

            if (serviceType == typeof(ILoggerFactory)) 
            { 
                return _settings.LoggerFactory;
            }

            if (serviceType == typeof(IMessagePublisher))
            {
                return _messagePublisher;
            }

            if (serviceType == typeof(IMessageConsumer))
            {
                return _messageConsumer;
            }

            if (serviceType == typeof(IJobSerializer))
            {
                return _jobSerializer;
            }

            if (serviceType == typeof(IJobTypeProvider))
            {
                return _jobTypeProvider;
            }

            if (serviceType == typeof(IWorkerCoordinator))
            {
                EnsureWorkerCoordinator();
                return _workerCoordinator;
            }

            return null;
        }

        public void Dispose()
        {
        }

        private void EnsureWorkerCoordinator()
        {
            lock (_singletonLock)
            {
                if (_workerCoordinator == null)
                {
                    _workerCoordinator = new WorkerCoordinator(_settings, _messageConsumer, this, _settings.LoggerFactory);
                }
            }
        }
    }

    public static class ServiceScopeExtensions
    {
        public static TService GetService<TService>(this IServiceScope scope)
        {
            return (TService)scope?.GetService(typeof(TService));
        }
    }
}
