using RabbitMQ.Client;
using RabbitMQ.Client.Events;

using MassiveJobs.Core;

namespace MassiveJobs.RabbitMqBroker
{
    public class RabbitMqMessageConsumer : IMessageConsumer
    {
        private readonly IModel _model;
        private readonly EventingBasicConsumer _consumer;
        private readonly ILogger _logger;

        public event MessageReceivedHandler MessageReceived;
        
        public RabbitMqMessageConsumer(IConnection connection, string queueName, ushort prefetchCount, ILogger logger)
        {
            _logger = logger;
            _model = connection.CreateModel();

            _model.BasicQos(0, prefetchCount, false);

            _consumer = new EventingBasicConsumer(_model);
            _consumer.Received += ConsumerOnReceived;

            _model.BasicConsume(_consumer, queueName, exclusive: false);
        }

        public void Dispose()
        {
            if (_consumer != null)
            {
                _consumer.Received -= ConsumerOnReceived;
            }

            _model.SafeClose(_logger);
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
                DeliveryTag = e.DeliveryTag,
                TypeTag = e.BasicProperties?.Type,
                Body = e.Body.ToArray()
            };

            MessageReceived(this, message);
        }
    }
}
