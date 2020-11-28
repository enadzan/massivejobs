using System;

namespace MassiveJobs.Core
{
    public class Jobs
    {
        private MassiveJobsSettings _settings;
        private IJobLoggerFactory _loggerFactory;

        private Func<IJobServiceProvider, IJobSerializer> _serializerFactory;
        private Func<IJobServiceProvider, IJobTypeProvider> _typeProviderFactory;
        private Func<IJobServiceProvider, IMessagePublisher> _publisherFactory;
        private Func<IJobServiceProvider, IMessageConsumer> _consumerFactory;

        private Jobs(MassiveJobsSettings settings)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        }

        public static Jobs Configure(MassiveJobsSettings settings = null)
        {
            return new Jobs(settings ?? new MassiveJobsSettings());
        }

        public static void Deinitialize()
        {
            MassiveJobsMediator.Deinitialize();
        }

        public Jobs WithLoggerFactory(IJobLoggerFactory loggerFactory)
        {
            _loggerFactory = loggerFactory;
            return this;
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

        public virtual void Initialize(bool startWorkers = true)
        {
            MassiveJobsMediator.Initialize(CreateServiceProvider());

            if (startWorkers)
            {
                MassiveJobsMediator.DefaultInstance.StartJobWorkers();
            }
        }

        internal MassiveJobsMediator InitializeNew()
        {
            return new MassiveJobsMediator(CreateServiceProvider());
        }

        private DefaultJobServiceProvider CreateServiceProvider()
        {
            if (MassiveJobsMediator.IsInitialized) throw new Exception("MassiveJobs already initialized");

            if (_consumerFactory == null) throw new Exception("Message consumer must be configured");
            if (_publisherFactory == null) throw new Exception("Message consumer must be configured");

            var provider = new DefaultJobServiceProvider(_settings, _loggerFactory);

            provider.RegisterServices(_publisherFactory, _consumerFactory, _serializerFactory, _typeProviderFactory);

            return provider;
        }
    }
}
