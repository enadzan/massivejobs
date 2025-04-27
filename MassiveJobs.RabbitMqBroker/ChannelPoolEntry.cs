using RabbitMQ.Client;
using Microsoft.Extensions.Logging;

namespace MassiveJobs.RabbitMqBroker
{
    public class ChannelPoolEntry
    {
        public IChannel Channel { get; }
        public BasicProperties BasicProperties { get; }

        public ChannelPoolEntry(IChannel channel, BasicProperties basicProperties)
        {
            Channel = channel;
            BasicProperties = basicProperties;
        }

        public bool IsOpen
        {
            get
            {
                return Channel.IsOpen;
            }
        }

        public void Close(ILogger logger)
        {
            Channel.SafeCloseAsync(logger).Wait();
        }
    }
}
