using MassiveJobs.Core;

namespace MassiveJobs.RabbitMqBroker
{
    public class RabbitMqMessageBrokerFactory : IMessageBrokerFactory
    {
        private readonly RabbitMqSettings _rabbitMqSettings;
        private readonly MassiveJobsSettings _massiveJobsSettings;

        public RabbitMqMessageBrokerFactory(RabbitMqSettings rabbitMqSettings, MassiveJobsSettings massiveJobsSettings)
        {
            _rabbitMqSettings = rabbitMqSettings;
            _massiveJobsSettings = massiveJobsSettings;
        }

        public IMessageBroker CreateMessageBroker(MessageBrokerType brokerType)
        {
            return new RabbitMqMessageBroker(
                _rabbitMqSettings, 
                _massiveJobsSettings, 
                brokerType == MessageBrokerType.JobPublisher ? true : false,
                _massiveJobsSettings.LoggerFactory.SafeCreateLogger<RabbitMqMessageBroker>());
        }
    }
}
