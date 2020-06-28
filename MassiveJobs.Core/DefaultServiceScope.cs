using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace MassiveJobs.Core
{
    public class DefaultServiceScopeFactory : IServiceScopeFactory
    {
        private readonly DefaultServiceCollection _serviceCollection;
        private readonly ILoggerFactory _loggerFactory;

        public IServiceCollection ServiceCollection => _serviceCollection;

        public DefaultServiceScopeFactory(MassiveJobsSettings settings)
        {
            _serviceCollection = new DefaultServiceCollection(settings);
            _loggerFactory = settings.LoggerFactory;
        }

        public IServiceScope CreateScope()
        {
            return new DefaultServiceScope(_serviceCollection, _loggerFactory);
        }

        public void Dispose()
        {
            _serviceCollection.SafeDispose();
        }
    }

    public class DefaultServiceScope : IServiceScope
    {
        private readonly DefaultServiceCollection _serviceCollection;
        private readonly ILoggerFactory _loggerFactory;
        private readonly ConcurrentBag<IJobPublisher> _publishers;

        public DefaultServiceScope(DefaultServiceCollection serviceCollection, ILoggerFactory loggerFactory)
        {
            _serviceCollection = serviceCollection;
            _loggerFactory = loggerFactory;
            _publishers = new ConcurrentBag<IJobPublisher>();
        }

        public object GetService(Type serviceType)
        {
            if (serviceType == typeof(IJobPublisher))
            {
                var publisher = new DefaultJobPublisher(
                    _serviceCollection.GetSingleton<MassiveJobsSettings>(),
                    _serviceCollection.GetSingleton<IMessagePublisher>(),
                    _serviceCollection.GetSingleton<IJobTypeProvider>(),
                    _serviceCollection.GetSingleton<IJobSerializer>(),
                    _loggerFactory.SafeCreateLogger<DefaultJobPublisher>()
                );

                _publishers.Add(publisher);

                return publisher;
            }

            if (serviceType == typeof(IJobRunner) 
                || serviceType == typeof(MassiveJobsSettings)
                || serviceType == typeof(IMessagePublisher)
                || serviceType == typeof(IMessageConsumer)
                || serviceType == typeof(IJobSerializer) 
                || serviceType == typeof(IJobTypeProvider)
                || serviceType == typeof(IWorkerCoordinator))
            {
                return _serviceCollection.GetSingleton(serviceType);
            }

            return null;
        }

        public void Dispose()
        {
            while (_publishers.Count > 0)
            {
                if (_publishers.TryTake(out var publisher)) publisher.SafeDispose();
            }
        }
    }

    public class DefaultServiceCollection : IServiceCollection
    {
        private readonly Dictionary<Type, object> _singletons = new Dictionary<Type, object>();

        public DefaultServiceCollection(MassiveJobsSettings settings)
        {
            AddSingleton(typeof(MassiveJobsSettings), settings);
            AddSingleton(typeof(IJobRunner), new DefaultJobRunner(settings.LoggerFactory.SafeCreateLogger<DefaultJobRunner>()));
            AddSingleton(typeof(IJobSerializer), new DefaultSerializer());
            AddSingleton(typeof(IJobTypeProvider), new DefaultTypeProvider());
        }

        public void AddSingleton(Type serviceType, object instance)
        {
            lock (_singletons)
            {
                _singletons[serviceType] = instance;
            }
        }

        public void Dispose()
        {
            var disposables = new List<IDisposable>();

            lock (_singletons)
            {
                foreach (var value in _singletons.Values)
                {
                    if (value is IDisposable disposable)
                    {
                        disposables.Add(disposable);
                    }
                }
                _singletons.Clear();
            }

            // Dispose out of lock, who knows what this might call
            foreach (var disposable in disposables)
            {
                disposable.SafeDispose();
            }
        }

        public object GetSingleton(Type type)
        {
            lock (_singletons)
            {
                _singletons.TryGetValue(type, out var service);
                return service;
            }
        }
    }

    public static class ServiceScopeExtensions
    {
        public static TService GetService<TService>(this IServiceScope scope)
        {
            return (TService)scope?.GetService(typeof(TService));
        }

        public static void AddSingleton<TService>(this IServiceCollection serviceCollection, TService instance)
        {
            serviceCollection.AddSingleton(typeof(TService), instance);
        }

        public static TService GetSingleton<TService>(this DefaultServiceCollection serviceCollection)
        {
            return (TService)serviceCollection.GetSingleton(typeof(TService));
        }
    }
}
