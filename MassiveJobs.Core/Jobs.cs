using System;
using MassiveJobs.Core.DependencyInjection;
using MassiveJobs.Core.Serialization;

namespace MassiveJobs.Core
{
    public class Jobs
    {
        private readonly DefaultJobServiceProvider _serviceProvider;

        private Func<IJobServiceProvider, IJobSerializer> _serializerFactory;
        private Func<IJobServiceProvider, IJobTypeProvider> _typeProviderFactory;
        private Func<IJobServiceProvider, IMessagePublisher> _publisherFactory;
        private Func<IJobServiceProvider, IMessageConsumer> _consumerFactory;

        public IJobServiceCollection ServiceCollection { get; }

        private Jobs(DefaultJobServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
            ServiceCollection = new DefaultServiceCollection(serviceProvider);
        }

        public static Jobs Configure(MassiveJobsSettings settings = null, IJobLoggerFactory loggerFactory = null)
        {
            return new Jobs(new DefaultJobServiceProvider(settings, loggerFactory));
        }

        public static void Deinitialize()
        {
            MassiveJobsMediator.Deinitialize();
        }

        public Jobs WithSerializer(Func<IJobServiceProvider, IJobSerializer> serializerFactory)
        {
            _serializerFactory = serializerFactory;
            return this;
        }

        public Jobs WithTypeProvider(Func<IJobServiceProvider, IJobTypeProvider> typeProviderFactory)
        {
            _typeProviderFactory = typeProviderFactory;
            return this;
        }

        public Jobs WithMessageBroker(
            Func<IJobServiceProvider, IMessagePublisher> publisherFactory,
            Func<IJobServiceProvider, IMessageConsumer> consumerFactory)
        {
            _publisherFactory = publisherFactory;
            _consumerFactory = consumerFactory;
            return this;
        }

        public void Initialize(bool startWorkers = true)
        {
            RegisterInstances();
            MassiveJobsMediator.Initialize(_serviceProvider);

            if (startWorkers)
            {
                MassiveJobsMediator.DefaultInstance.StartJobWorkers();
            }
        }

        internal MassiveJobsMediator InitializeNew()
        {
            RegisterInstances();
            return new MassiveJobsMediator(_serviceProvider);
        }

        private void RegisterInstances()
        {
            if (_publisherFactory == null) throw new ArgumentNullException($"{nameof(IMessagePublisher)} must be registered");
            if (_consumerFactory == null) throw new ArgumentNullException($"{nameof(IMessageConsumer)} must be registered");

            ServiceCollection.RegisterInstance(_typeProviderFactory ?? (_ => new DefaultTypeProvider()));
            ServiceCollection.RegisterInstance(_serializerFactory ?? (_ => new DefaultSerializer()));
            ServiceCollection.RegisterInstance(_publisherFactory);
            ServiceCollection.RegisterInstance(_consumerFactory);
        }
    }
}
