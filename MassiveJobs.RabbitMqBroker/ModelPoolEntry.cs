using RabbitMQ.Client;
using Microsoft.Extensions.Logging;

namespace MassiveJobs.RabbitMqBroker
{
    public class ModelPoolEntry
    {
        public IModel Model { get; }
        public IBasicProperties BasicProperties { get; }

        public ModelPoolEntry(IModel model, IBasicProperties basicProperties)
        {
            Model = model;
            BasicProperties = basicProperties;
        }

        public bool IsOpen
        {
            get
            {
                return Model.IsOpen;
            }
        }

        public void Close(ILogger logger)
        {
            Model.SafeClose(logger);
        }
    }
}
