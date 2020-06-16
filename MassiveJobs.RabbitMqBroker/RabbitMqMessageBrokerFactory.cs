using MassiveJobs.Core;

namespace MassiveJobs.RabbitMqBroker
{
    class RabbitMqMessageBrokerFactory : IMessageBrokerFactory
    {
        private readonly RabbitMqSettings _rabbitMqSettings;
        private readonly MassiveJobsSettings _massiveJobsSettings;

        public RabbitMqMessageBrokerFactory(RabbitMqSettings rabbitMqSettings, MassiveJobsSettings massiveJobsSettings)
        {
            _rabbitMqSettings = rabbitMqSettings;
            _massiveJobsSettings = massiveJobsSettings;
        }

        public IMessageBroker CreateMessageBroker()
        {
            return new RabbitMqMessageBroker(_rabbitMqSettings, _massiveJobsSettings, _massiveJobsSettings.LoggerFactory.SafeCreateLogger<RabbitMqMessageBroker>());
        }
    }
}
