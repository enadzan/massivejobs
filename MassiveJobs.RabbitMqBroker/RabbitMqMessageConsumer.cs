using RabbitMQ.Client;
using RabbitMQ.Client.Events;

using MassiveJobs.Core;

namespace MassiveJobs.RabbitMqBroker
{
    public class RabbitMqMessageConsumer : RabbitMqMessageBroker, IMessageConsumer
    {
        private readonly ushort _prefetchCount;

        public event MessageConsumerDisconnected Disconnected;

        public RabbitMqMessageConsumer(RabbitMqSettings rmqSettings, MassiveJobsSettings jobsSettings, IJobLogger logger)
            : base(rmqSettings, jobsSettings, false, logger)
        {
            _prefetchCount = rmqSettings.PrefetchCount;
        }

        public void Connect()
        {
            EnsureConnectionExists();
        }

        public IMessageReceiver CreateReceiver(string queueName)
        {
            EnsureConnectionExists();
            return new MessageReceiver(Connection, queueName, _prefetchCount);
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

            public event MessageReceivedHandler MessageReceived;

            public MessageReceiver(IConnection connection, string queueName, ushort prefetchCount)
            {
                _queueName = queueName;

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

            public void AckMessageProcessed(ulong deliveryTag)
            {
                _model.BasicAck(deliveryTag, false);
            }

            private void ConsumerOnReceived(object sender, BasicDeliverEventArgs e)
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

            public void Start()
            {
                _model.BasicConsume(_consumer, _queueName, exclusive: false);
            }
        }
    }
}
