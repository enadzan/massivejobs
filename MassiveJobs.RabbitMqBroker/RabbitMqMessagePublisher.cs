using System;

using MassiveJobs.Core;
using RabbitMQ.Client;

namespace MassiveJobs.RabbitMqBroker
{
    public class RabbitMqMessagePublisher : IMessagePublisher
    {
        private readonly IModel _model;
        private readonly IBasicProperties _props;

        public bool IsOk => _model.IsOpen;

        public RabbitMqMessagePublisher(IConnection connection)
        {
            _model = connection.CreateModel();
            _model.ConfirmSelect();

            _props = _model.CreateBasicProperties();
        }

        public void Dispose()
        {
            _model.SafeClose();
            _model.SafeDispose();
        }

        public void Publish(string exchangeName, string routingKey, ReadOnlyMemory<byte> body, string typeTag, bool persistent)
        {
            _props.Type = typeTag;
            _props.Persistent = persistent;

            _model.BasicPublish(exchangeName, routingKey, _props, body);
        }

        public void WaitForConfirmsOrDie(TimeSpan timeout)
        {
            _model.WaitForConfirmsOrDie(timeout);
        }
    }
}
