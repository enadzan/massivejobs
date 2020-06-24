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
        private readonly ILogger _logger;

        public bool IsOk => _model.IsOpen;

        public RabbitMqMessagePublisher(IConnection connection, RabbitMqSettings settings, ILogger logger)
        {
            _settings = settings;
            _logger = logger;
            _model = connection.CreateModel();
            _model.ConfirmSelect();

            _props = _model.CreateBasicProperties();
        }

        public void Dispose()
        {
            _model.SafeClose(_logger);
        }

        public void Publish(string routingKey, ReadOnlyMemory<byte> body, string typeTag, bool persistent)
        {
            _props.Type = typeTag;
            _props.Persistent = persistent;

            _model.BasicPublish(_settings.ExchangeName, routingKey, _props, body);
        }

        public void WaitForConfirmsOrDie(TimeSpan timeout)
        {
            _model.WaitForConfirmsOrDie(timeout);
        }
    }
}
