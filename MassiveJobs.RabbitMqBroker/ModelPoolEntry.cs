using System;
using RabbitMQ.Client;

namespace MassiveJobs.RabbitMqBroker
{
    public class ModelPoolEntry: IDisposable
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

        public void Dispose()
        {
            Model.SafeClose();
        }
    }
}
