using System;
using System.Collections.Generic;
using System.Threading;

namespace MassiveJobs.Core.Memory
{
    public class InMemoryMessagePublisher : IMessagePublisher
    {
        private readonly MassiveJobsSettings _settings;
        private readonly InMemoryMessages _messages;

        public InMemoryMessagePublisher(MassiveJobsSettings settings, InMemoryMessages messages)
        {
            _messages = messages;
            _messages.EnsureQueues(settings);

            _settings = settings;
        }

        public void Publish(string routingKey, IEnumerable<RawMessage> messages, TimeSpan timeout)
        {
            if (routingKey == _settings.StatsQueueName) return;

            foreach (var msg in messages)
            {
                _messages.EnqueueMessage(routingKey, msg, _settings.MaxQueueLength);
            }
        }

        public int GetJobCount(string queueName)
        {
            return _messages.GetCount(queueName);
        }

        public void Dispose()
        {
        }
    }

    public class InMemoryMessageConsumer : IMessageConsumer
    {
        private readonly InMemoryMessages _messages;
        private readonly IJobLoggerFactory _loggerFactory;

#pragma warning disable CS0067
        public event MessageConsumerDisconnected Disconnected;
#pragma warning restore CS0067

        public InMemoryMessageConsumer(MassiveJobsSettings settings, InMemoryMessages messages, IJobLoggerFactory loggerFactory)
        {
            _messages = messages;
            _messages.EnsureQueues(settings);

            _loggerFactory = loggerFactory;
        }

        public void Dispose()
        {
        }

        public void Connect()
        {
        }

        public IMessageReceiver CreateReceiver(string queueName)
        {
            return new InMemoryMessageReceiver(_messages, queueName, _loggerFactory.CreateLogger<InMemoryMessageReceiver>());
        }
    }

    class InMemoryMessageReceiver : IMessageReceiver
    {
        private readonly InMemoryMessages _messages;
        private readonly string _queueName;
        private readonly IJobLogger<InMemoryMessageReceiver> _logger;
        private readonly Thread _consumerThread;

        private volatile bool _disposed;

        public event MessageReceivedHandler MessageReceived;

        public InMemoryMessageReceiver(InMemoryMessages messages, string queueName, IJobLogger<InMemoryMessageReceiver> logger)
        {
            _messages = messages;
            _queueName = queueName;
            _logger = logger;
            _consumerThread = new Thread(ConsumerFunction) { IsBackground = true };
        }

        public void Start()
        {
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
            try
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
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in consumer function");
            }
        }

        public void Dispose()
        {
            _disposed = true;
        }
    }
}
