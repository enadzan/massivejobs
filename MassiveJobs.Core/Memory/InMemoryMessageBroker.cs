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
            _settings = settings;
            _messages = messages;

            for (var i = 0; i < _settings.ImmediateWorkersCount; i++)
            {
                _messages.EnsureQueue(string.Format(_settings.ImmediateQueueNameTemplate, i));
            }

            for (var i = 0; i < _settings.ScheduledWorkersCount; i++)
            {
                _messages.EnsureQueue(string.Format(_settings.ScheduledQueueNameTemplate, i));
            }

            for (var i = 0; i < _settings.PeriodicWorkersCount; i++)
            {
                _messages.EnsureQueue(string.Format(_settings.PeriodicQueueNameTemplate, i));
            }

            for (var i = 0; i < _settings.LongRunningWorkersCount; i++)
            {
                _messages.EnsureQueue(string.Format(_settings.LongRunningQueueNameTemplate, i));
            }

            _messages.EnsureQueue(_settings.ErrorQueueName);
            _messages.EnsureQueue(_settings.FailedQueueName);
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

#pragma warning disable CS0067
        public event MessageConsumerDisconnected Disconnected;
#pragma warning restore CS0067

        public InMemoryMessageConsumer(InMemoryMessages messages)
        {
            _messages = messages;
        }

        public void Dispose()
        {
        }

        public void Connect()
        {
        }

        public IMessageReceiver CreateReceiver(string queueName)
        {
            return new InMemoryMessageReceiver(_messages, queueName);
        }
    }

    class InMemoryMessageReceiver : IMessageReceiver
    {
        private readonly InMemoryMessages _messages;
        private readonly string _queueName;
        private readonly Thread _consumerThread;

        private volatile bool _disposed;

        public event MessageReceivedHandler MessageReceived;

        public InMemoryMessageReceiver(InMemoryMessages messages, string queueName)
        {
            _messages = messages;
            _queueName = queueName;
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
