using System.Collections.Generic;
using System.Threading;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;

namespace MassiveJobs.Core
{
    public abstract class Worker : BatchProcessor<RawMessage>, IWorker
    {
        private volatile IMessageConsumer _messageConsumer;

        protected readonly IMessageBroker MessageBroker;
        protected readonly string QueueName;
        protected readonly IJobPublisher JobPublisher;
        protected readonly IJobRunner JobRunner;
        protected readonly IJobSerializer Serializer;
        protected readonly IJobTypeProvider TypeProvider;
        protected readonly IServiceScopeFactory ServiceScopeFactory;
       
        protected abstract void ProcessMessageBatch(List<RawMessage> messages, IServiceScope serviceScope, CancellationToken cancellationToken);

        protected Worker(
            IMessageBroker messageBroker,
            string queueName,
            int batchSize,
            IJobPublisher jobPublisher,
            IJobRunner jobRunner,
            IJobSerializer serializer,
            IJobTypeProvider typeProvider,
            IServiceScopeFactory scopeFactory,
            ILogger logger)
            : base(batchSize, logger)
        {
            MessageBroker = messageBroker;
            QueueName = queueName;
            JobPublisher = jobPublisher;
            JobRunner = jobRunner;
            Serializer = serializer;
            TypeProvider = typeProvider;
            ServiceScopeFactory = scopeFactory;
        }

        protected override void OnStarted()
        {
            base.OnStarted();
            CreateConsumer();
        }

        protected override void OnStopping()
        {
            DisposeConsumer();
            base.OnStopping();
        }

        protected void CreateConsumer()
        {
            if (_messageConsumer != null) return;

            _messageConsumer = MessageBroker.CreateConsumer(QueueName);
            _messageConsumer.MessageReceived += ConsumerOnMessageReceived;
        }

        protected void DisposeConsumer()
        {
            if (_messageConsumer == null) return;

            _messageConsumer.MessageReceived -= ConsumerOnMessageReceived;
            _messageConsumer.SafeDispose();
            _messageConsumer = null;
        }

        protected bool TryDeserializeJob(RawMessage rawMessage, out JobInfo job)
        {
            job = null;

            var argsTag = rawMessage.TypeTag;
            if (argsTag == null || argsTag == string.Empty) return false;

            job = Serializer.Deserialize(rawMessage.Body, argsTag, TypeProvider);
            return job != null;
        }

        protected override void ProcessMessageBatch(List<RawMessage> messages, CancellationToken cancellationToken)
        {
            if (messages.Count > 0)
            {
                var serviceScope = ServiceScopeFactory.SafeCreateScope();
                try
                {
                    ProcessMessageBatch(messages, serviceScope, cancellationToken);
                }
                finally
                {
                    serviceScope.SafeDispose();
                }
            }
        }

        protected void OnBatchProcessed(ulong lastDeliveryTag)
        {
            _messageConsumer.AckBatchProcessed(lastDeliveryTag);
        }

        protected void OnMessageProcessed(ulong deliveryTag)
        {
            _messageConsumer.AckMessageProcessed(deliveryTag);
        }

        private void ConsumerOnMessageReceived(IMessageConsumer sender, RawMessage message)
        {
            // constructing string is expensive and this is hot path
            if (Logger.IsEnabled(LogLevel.Trace))
            {
                Logger.LogTrace($"Message received on worker {QueueName}");
            }

            AddMessage(message, 0);
        }
    }
}
