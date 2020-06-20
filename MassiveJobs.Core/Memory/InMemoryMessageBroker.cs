using System;
using System.Threading;

namespace MassiveJobs.Core.Memory
{
    class InMemoryMessageBrokerFactory : IMessageBrokerFactory
    {
        private readonly MassiveJobsSettings _settings;

        public InMemoryMessages Messages { get; }

        public InMemoryMessageBrokerFactory(MassiveJobsSettings settings)
        {
            _settings = settings;
            Messages = new InMemoryMessages();
        }

        public IMessageBroker CreateMessageBroker()
        {
            return new InMemoryMessageBroker(_settings, Messages);
        }
    }

    class InMemoryMessageBroker : IMessageBroker
    {
        private readonly MassiveJobsSettings _settings;
        private readonly InMemoryMessages _messages;

        public InMemoryMessageBroker(MassiveJobsSettings settings, InMemoryMessages messages)
        {
            _settings = settings;
            _messages = messages;
        }

        public IMessageConsumer CreateConsumer(string queueName)
        {
            return new InMemoryMessageConsumer(queueName, _messages);
        }

        public IMesagePublisher CreatePublisher()
        {
            return new InMemoryMessagePublisher(_settings, _messages);
        }

        public void DeclareTopology()
        {
            for (var i = 0; i < _settings.ImmediateWorkersCount; i++)
            {
                _messages.EnsureQueue(string.Format(_settings.ImmediateQueueNameTemplate, i));
            }

            for (var i = 0; i < _settings.ScheduledWorkersCount; i++)
            {
                _messages.EnsureQueue(string.Format(_settings.ScheduledQueueNameTemplate, i));
            }

            _messages.EnsureQueue(_settings.ErrorQueueName);
            _messages.EnsureQueue(_settings.FailedQueueName);
        }

        public int GetJobCount(string queueName)
        {
            return _messages.GetCount(queueName);
        }

        public void Dispose()
        {
        }
    }

    class InMemoryMessagePublisher : IMesagePublisher
    {
        private readonly MassiveJobsSettings _settings;
        private readonly InMemoryMessages _messages;

        public bool IsOk => true;

        public InMemoryMessagePublisher(MassiveJobsSettings settings, InMemoryMessages messages)
        {
            _settings = settings;
            _messages = messages;
        }

        public void Dispose()
        {
        }

        public void Publish(string exchangeName, string routingKey, ReadOnlyMemory<byte> body, string typeTag, bool persistent)
        {
            if (routingKey == _settings.StatsQueueName) return;

            var rawMessage = new RawMessage
            {
                Body = body.ToArray(),
                TypeTag = typeTag,
            };

            _messages.EnqueueMessage(routingKey, rawMessage, _settings.MaxQueueLength);
        }

        public void WaitForConfirmsOrDie(TimeSpan timeout)
        {
        }
    }

    class InMemoryMessageConsumer : IMessageConsumer
    {
        private readonly string _queueName;
        private readonly InMemoryMessages _messages;
        private readonly Thread _consumerThread;

        private volatile bool _disposed;

        public event MessageReceivedHandler MessageReceived;

        public InMemoryMessageConsumer(string queueName, InMemoryMessages messages)
        {
            _queueName = queueName;
            _messages = messages;

            _consumerThread = new Thread(ConsumerFunction) { IsBackground = true };
            _consumerThread.Start();
        }
        
        public void AckBatchProcessed(ulong lastDeliveryTag)
        {
            _messages.RemoveBatch(_queueName, lastDeliveryTag);
        }

        public void AckMessageProcessed(ulong deliveryTag)
        {
            _messages.RemoveMessage(_queueName, deliveryTag);
        }
        
        public void ConsumerFunction()
        {
            ulong lastReceivedTag = 0;

            while (!_disposed)
            {
                if (_messages.GetMessages(_queueName, lastReceivedTag, out var batch))
                {
                    foreach (var msg in batch)
                    {
                        if (_disposed) break;

                        MessageReceived?.Invoke(this, msg);
                        lastReceivedTag = msg.DeliveryTag;
                    }
                }
            }
        }

        public void Dispose()
        {
            _disposed = true;
        }
    }
}
