using System;
using System.Collections.Generic;
using System.Threading;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;

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
        private readonly ILoggerFactory _loggerFactory;

#pragma warning disable CS0067
        public event MessageConsumerDisconnected Disconnected;
#pragma warning restore CS0067

        public InMemoryMessageConsumer(MassiveJobsSettings settings, InMemoryMessages messages, ILoggerFactory loggerFactory)
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
        private readonly ILogger<InMemoryMessageReceiver> _logger;
        private readonly Thread _consumerThread;

        private volatile int _disposed;
        private volatile int _started;

        private readonly ManualResetEvent _stopEvent = new ManualResetEvent(false);

        public event MessageReceivedHandler MessageReceived;

        public InMemoryMessageReceiver(InMemoryMessages messages, string queueName, ILogger<InMemoryMessageReceiver> logger)
        {
            _messages = messages;
            _queueName = queueName;
            _logger = logger;
            _consumerThread = new Thread(ConsumerFunction) { IsBackground = true };
        }

        public void Start()
        {
            var previousValue = Interlocked.Exchange(ref _started, 1);
            if (previousValue != 0) return;

            _consumerThread.Start();
        }

        public void AckBatchProcessed(ulong lastDeliveryTag)
        {
            // nothing to do
        }

        public void AckMessageProcessed(IServiceScope scope, ulong deliveryTag)
        {
            _messages.RemoveMessage(_queueName, deliveryTag);
        }
        
        public void ConsumerFunction()
        {
            try
            {
                // consumer function will run at least once (useful for tests)
                do
                {
                    if (_messages.GetMessages(_queueName, out var batch))
                    {
                        foreach (var msg in batch)
                        {
                            MessageReceived?.Invoke(this, msg);
                        }
                    }
                }
                while (_disposed == 0);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in consumer function");
            }

            _messages.MoveUnackToReady(_queueName);
            _stopEvent.Set();
        }

        public void Dispose()
        {
            var previousValue = Interlocked.Exchange(ref _disposed, 1);
            if (previousValue != 0) return;

            _stopEvent.WaitOne();
        }

        public void AckBatchMessageProcessed(IServiceScope scope, ulong deliveryTag)
        {
            _messages.RemoveMessage(_queueName, deliveryTag);
        }

        public IBrokerTransaction BeginTransaction(IServiceScope scope)
        {
            return null;
        }
    }
}
