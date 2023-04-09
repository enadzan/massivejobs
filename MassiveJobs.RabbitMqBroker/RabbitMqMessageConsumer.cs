using System;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using RabbitMQ.Client;
using RabbitMQ.Client.Events;

using MassiveJobs.Core;

namespace MassiveJobs.RabbitMqBroker
{
    public class RabbitMqMessageConsumer : RabbitMqMessageBroker, IMessageConsumer
    {
        private readonly ILoggerFactory _loggerFactory;
        private readonly ushort _prefetchCount;

        public event MessageConsumerDisconnected Disconnected;

        public RabbitMqMessageConsumer(RabbitMqSettings rmqSettings,
            MassiveJobsSettings jobsSettings,
            ILoggerFactory loggerFactory,
            ILogger<RabbitMqMessageConsumer> consumerLogger)
            : base(rmqSettings, jobsSettings, false, consumerLogger)
        {
            _loggerFactory = loggerFactory;
            _prefetchCount = rmqSettings.PrefetchCount;
        }

        public void Connect()
        {
            EnsureConnectionExists();
        }

        public IMessageReceiver CreateReceiver(string queueName)
        {
            EnsureConnectionExists();

            return new MessageReceiver(
                Connection,
                queueName,
                _prefetchCount,
                _loggerFactory.CreateLogger<MessageReceiver>()
            );
        }

        protected override void OnDisconnected()
        {
            Disconnected?.Invoke(this);
        }

        private class MessageReceiver : IMessageReceiver
        {
            private readonly IModel _model;
            private readonly EventingBasicConsumer _consumer;
            private readonly string _queueName;
            private readonly ILogger<MessageReceiver> _logger;

            public event MessageReceivedHandler MessageReceived;

            public MessageReceiver(IConnection connection, string queueName, ushort prefetchCount,
                ILogger<MessageReceiver> logger)
            {
                _queueName = queueName;
                _logger = logger;

                _model = connection.CreateModel();
                _model.BasicQos(0, prefetchCount, false);

                _consumer = new EventingBasicConsumer(_model);
                _consumer.Received += ConsumerOnReceived;
            }

            public void Dispose()
            {
                if (_consumer != null)
                {
                    _consumer.Received -= ConsumerOnReceived;
                }

                _model.SafeClose();
            }

            public void AckBatchProcessed(ulong lastDeliveryTag)
            {
                _model.BasicAck(lastDeliveryTag, true);
            }

            public void AckMessageProcessed(IServiceScope scope, ulong deliveryTag)
            {
                _model.BasicAck(deliveryTag, false);
            }

            public void AckBatchMessageProcessed(IServiceScope scope, ulong deliveryTag)
            {
            }

            private void ConsumerOnReceived(object sender, BasicDeliverEventArgs e)
            {
                try
                {
                    if (MessageReceived == null) return;

                    var message = new RawMessage
                    {
                        TypeTag = e.BasicProperties?.Type,
                        IsPersistent = e.BasicProperties?.Persistent ?? false,
                        Body = e.Body.ToArray(),
                        DeliveryTag = e.DeliveryTag
                    };

                    MessageReceived(this, message);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Error in {nameof(ConsumerOnReceived)}");
                }
            }

            public void Start()
            {
                _model.BasicConsume(_consumer, _queueName, exclusive: false);
            }

            public IBrokerTransaction BeginTransaction(IServiceScope scope)
            {
                return null;
            }
        }
    }
}
