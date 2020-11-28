﻿using RabbitMQ.Client;

using MassiveJobs.Core;

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

        public void Close(IJobLogger logger)
        {
            Model.SafeClose(logger);
        }
    }
}
