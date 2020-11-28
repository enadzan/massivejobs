using System;

namespace MassiveJobs.Core
{
    public class Jobs
    {
        public MassiveJobsSettings Settings { get; }

        private IJobLoggerFactory _loggerFactory;
        private IJobSerializer _serializer;
        private IJobTypeProvider _typeProvider;
        private IMessagePublisher _messagePublisher;
        private IMessageConsumer _messageConsumer;

        private Jobs(MassiveJobsSettings settings)
        {
            Settings = settings ?? throw new ArgumentNullException(nameof(settings));
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

        public Jobs WithSerializer(IJobSerializer serializer)
        {
            _serializer = serializer;
            return this;
        }

        public Jobs WithTypeProvider(IJobTypeProvider typeProvider)
        {
            _typeProvider = typeProvider;
            return this;
        }

        public Jobs WithMessagePublisher(IMessagePublisher messagePublisher)
        {
            _messagePublisher = messagePublisher;
            return this;
        }

        public Jobs WithMessageConsumer(IMessageConsumer messageConsumer)
        {
            _messageConsumer = messageConsumer;
            return this;
        }

        public void Initialize()
        {
            MassiveJobsMediator.Initialize(CreateServiceProvider());
        }

        internal MassiveJobsMediator InitializeNew()
        {
            return new MassiveJobsMediator(CreateServiceProvider());
        }

        private DefaultJobServiceProvider CreateServiceProvider()
        {
            if (MassiveJobsMediator.IsInitialized) throw new Exception("MassiveJobs already initialized");

            if (_messageConsumer == null) throw new Exception("Message consumer must be configured");
            if (_messagePublisher == null) throw new Exception("Message consumer must be configured");

            return new DefaultJobServiceProvider(
                Settings,
                _messagePublisher,
                _messageConsumer,
                _loggerFactory,
                _serializer,
                _typeProvider);
        }
    }
}
