using System;
using System.Collections.Concurrent;

using MassiveJobs.Core.Serialization;

namespace MassiveJobs.Core
{
    public class DefaultServiceScopeFactory : IJobServiceScopeFactory
    {
        internal MassiveJobsSettings Settings { get; }
        internal IMessagePublisher MessagePublisher { get; }
        internal IMessageConsumer MessageConsumer { get; }
        internal IJobLoggerFactory JobLoggerFactory { get; }
        internal IJobSerializer JobSerializer { get; }
        internal IJobTypeProvider JobTypeProvider { get; }
        internal IJobRunner JobRunner { get; }

        public DefaultServiceScopeFactory(
            MassiveJobsSettings settings,
            IMessagePublisher messagePublisher,
            IMessageConsumer messageConsumer,
            IJobLoggerFactory jobLoggerFactory = null,
            IJobSerializer jobSerializer = null,
            IJobTypeProvider jobTypeProvider = null,
            IJobRunner jobRunner = null
            )
        {
            Settings = settings;
            MessagePublisher = messagePublisher;
            MessageConsumer = messageConsumer;
            JobLoggerFactory = jobLoggerFactory;
            JobSerializer = jobSerializer ?? new DefaultSerializer();
            JobTypeProvider = jobTypeProvider ?? new DefaultTypeProvider();
            JobRunner = jobRunner ?? new DefaultJobRunner(JobLoggerFactory.SafeCreateLogger<DefaultJobRunner>());
        }

        public IJobServiceScope CreateScope()
        {
            return new DefaultServiceScope(this);
        }

        public void Dispose()
        {
        }
    }

    public class DefaultServiceScope : IJobServiceScope
    {
        private readonly ConcurrentBag<IJobPublisher> _publishers;
        private readonly DefaultServiceScopeFactory _factory;

        public DefaultServiceScope(DefaultServiceScopeFactory serviceScopeFactory)
        {
            _factory = serviceScopeFactory;
            _publishers = new ConcurrentBag<IJobPublisher>();
        }

        public object GetRequiredService(Type serviceType)
        {
            var svc = GetService(serviceType);
            return svc ?? throw new ArgumentException($"Service of type {serviceType?.AssemblyQualifiedName} is not registered.");
        }

        public virtual object GetService(Type serviceType)
        {
            if (serviceType == typeof(IJobPublisher))
            {
                var publisher = new DefaultJobPublisher(
                    _factory.Settings,
                    _factory.MessagePublisher,
                    _factory.JobTypeProvider,
                    _factory.JobSerializer,
                    _factory.JobLoggerFactory.SafeCreateLogger<DefaultJobPublisher>()
                );

                _publishers.Add(publisher);

                return publisher;
            }

            if (serviceType == typeof(IJobRunner)) return _factory.JobRunner;
            if (serviceType == typeof(MassiveJobsSettings)) return _factory.Settings;
            if (serviceType == typeof(IMessagePublisher)) return _factory.MessagePublisher;
            if (serviceType == typeof(IMessageConsumer)) return _factory.MessageConsumer;
            if (serviceType == typeof(IJobSerializer)) return _factory.JobSerializer;
            if (serviceType == typeof(IJobTypeProvider)) return _factory.JobTypeProvider;

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

    public static class ServiceScopeExtensions
    {
        public static TService GetService<TService>(this IJobServiceScope scope)
        {
            return (TService)scope?.GetService(typeof(TService));
        }

        public static TService GetRequiredService<TService>(this IJobServiceScope scope)
        {
            return (TService)scope?.GetRequiredService(typeof(TService));
        }
    }
}
