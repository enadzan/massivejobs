using System;
using System.Collections.Generic;

using RabbitMQ.Client;

using MassiveJobs.Core;

namespace MassiveJobs.RabbitMqBroker
{
    public class RabbitMqMessagePublisher : RabbitMqMessageBroker, IMessagePublisher
    {
        public RabbitMqMessagePublisher(RabbitMqSettings rmqSettings, MassiveJobsSettings jobsSettings, IJobLogger logger)
            : base(rmqSettings, jobsSettings, true, logger)
        {
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
                    poolEntry.Model.BasicPublish(ExchangeName, routingKey, poolEntry.BasicProperties, msg.Body);
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
