using System;
using System.Collections.Generic;

using Microsoft.Extensions.Logging;

using RabbitMQ.Client;

namespace MassiveJobs.RabbitMqBroker
{
    public class ModelPool: IDisposable
    {
        private readonly ILogger _logger;
        private readonly IConnection _connection;
        private readonly int _maxRetained;
        private readonly Queue<ModelPoolEntry> _models;

        public ModelPool(IConnection connection, int maxRetained, ILogger logger)
        {
            _connection = connection;
            _maxRetained = maxRetained;
            _logger = logger;
            _models = new Queue<ModelPoolEntry>();
        }

        public ModelPoolEntry Get()
        {
            lock (_models)
            {
                if (_models.Count > 0) return _models.Dequeue();
            }

            var model = _connection.CreateModel();
            model.ConfirmSelect();

            var props = model.CreateBasicProperties();

            return new ModelPoolEntry(model, props);
        }

        public void Return(ModelPoolEntry pooledModel)
        {
            lock (_models)
            {
                if (_models.Count < _maxRetained)
                {
                    _models.Enqueue(pooledModel);
                    return;
                }
            }

            pooledModel.Close(_logger);
        }

        public bool AllOk()
        {
            lock (_models)
            {
                foreach (var model in _models)
                {
                    if (!model.IsOpen) return false;
                }
            }

            return true;
        }

        public void Dispose()
        {
            lock (_models)
            {
                while (_models.Count > 0)
                {
                    _models
                        .Dequeue()
                        .Close(_logger);
                }
            }
        }
    }
}
