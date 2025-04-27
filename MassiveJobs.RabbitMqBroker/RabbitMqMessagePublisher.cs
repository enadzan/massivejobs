using System;
using System.Collections.Generic;

using Microsoft.Extensions.Logging;

using MassiveJobs.Core;
using System.Threading.Tasks;

namespace MassiveJobs.RabbitMqBroker
{
    public class RabbitMqMessagePublisher : RabbitMqMessageBroker, IMessagePublisher
    {
        public RabbitMqMessagePublisher(RabbitMqSettings rmqSettings, MassiveJobsSettings jobsSettings, ILogger<RabbitMqMessagePublisher> logger)
            : base(rmqSettings, jobsSettings, true, logger)
        {
        }

        public void Publish(string routingKey, IEnumerable<RawMessage> messages, TimeSpan timeout)
        {
            EnsureConnectionExists();

            var poolEntry = ChannelPool.Get();
            try
            {
                var tasks = new List<Task>();

                foreach (var msg in messages)
                {
                    poolEntry.BasicProperties.Type = msg.TypeTag;
                    poolEntry.BasicProperties.Persistent = msg.IsPersistent;

                    var task = poolEntry.Channel.BasicPublishAsync(ExchangeName, routingKey, true, poolEntry.BasicProperties, msg.Body);

                    tasks.Add(task.AsTask());
                }

                bool completed = Task.WaitAll(tasks.ToArray(), timeout);

                if (!completed)
                {
                    throw new TimeoutException("Timeout waiting for batch publish to complete");
                }
            }
            finally
            {
                ChannelPool.Return(poolEntry);
            }
        }
    }
}
