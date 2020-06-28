using System;
using System.Collections.Generic;

using RabbitMQ.Client;

using MassiveJobs.Core;

namespace MassiveJobs.RabbitMqBroker
{
    public class RabbitMqMessagePublisher : RabbitMqMessageBroker, IMessagePublisher
    {
        private readonly RabbitMqSettings _rmqSettings;

        public RabbitMqMessagePublisher(RabbitMqSettings rmqSettings, MassiveJobsSettings jobsSettings, ILogger logger = null)
            : base(rmqSettings, jobsSettings, true, logger ?? jobsSettings.LoggerFactory.SafeCreateLogger<RabbitMqMessagePublisher>())
        {
            _rmqSettings = rmqSettings;
        }

        public void Publish(string routingKey, IEnumerable<RawMessage> messages, TimeSpan timeout)
        {
            EnsureConnectionExists();

            var poolEntry = ModelPool.Get();
            try
            {
                foreach (var msg in messages)
                {
                    poolEntry.BasicProperties.Type = msg.TypeTag;
                    poolEntry.BasicProperties.Persistent = msg.IsPersistent;
                    poolEntry.Model.BasicPublish(_rmqSettings.ExchangeName, routingKey, poolEntry.BasicProperties, msg.Body);
                }

                poolEntry.Model.WaitForConfirmsOrDie(timeout);
            }
            finally
            {
                ModelPool.Return(poolEntry);
            }
        }
    }
}
