using System;

using MassiveJobs.Core;
using RabbitMQ.Client;

namespace MassiveJobs.RabbitMqBroker
{
    public class RabbitMqMessagePublisher : IMessagePublisher
    {
        private readonly IModel _model;
        private readonly IBasicProperties _props;
        private readonly RabbitMqSettings _settings;

        public bool IsOk => _model.IsOpen;

        public RabbitMqMessagePublisher(IConnection connection, RabbitMqSettings settings)
        {
            _settings = settings;

            _model = connection.CreateModel();
            _model.ConfirmSelect();

            _props = _model.CreateBasicProperties();
        }

        public void Dispose()
        {
            _model.SafeClose();
            _model.SafeDispose();
        }

        public void Publish(string routingKey, ReadOnlyMemory<byte> body, string typeTag, bool persistent)
        {
            _props.Type = typeTag;
            _props.Persistent = persistent;

            _model.BasicPublish(GetExchangeName(routingKey), routingKey, _props, body);
        }

        public void WaitForConfirmsOrDie(TimeSpan timeout)
        {
            _model.WaitForConfirmsOrDie(timeout);
        }

        protected virtual string GetExchangeName(string routingKey)
        {
            return _settings.ExchangeName;
        }
    }
}
