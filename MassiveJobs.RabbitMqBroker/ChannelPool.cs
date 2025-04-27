using System;
using System.Collections.Generic;

using Microsoft.Extensions.Logging;

using RabbitMQ.Client;

namespace MassiveJobs.RabbitMqBroker
{
    public class ChannelPool: IDisposable
    {
        private readonly ILogger _logger;
        private readonly IConnection _connection;
        private readonly int _maxRetained;
        private readonly int _batchSize;
        private readonly Queue<ChannelPoolEntry> _models;

        public ChannelPool(IConnection connection, int maxRetained, int batchSize, ILogger logger)
        {
            _connection = connection;
            _maxRetained = maxRetained;
            _batchSize = batchSize;
            _logger = logger;
            _models = new Queue<ChannelPoolEntry>();
        }

        public ChannelPoolEntry Get()
        {
            lock (_models)
            {
                if (_models.Count > 0) return _models.Dequeue();
            }

            var channel = _connection.CreateChannelAsync(new CreateChannelOptions(
                publisherConfirmationsEnabled: true,
                publisherConfirmationTrackingEnabled: true,
                outstandingPublisherConfirmationsRateLimiter: new ThrottlingRateLimiter(_batchSize * 2)
            )).Result;

            return new ChannelPoolEntry(channel, new BasicProperties());
        }

        public void Return(ChannelPoolEntry pooledModel)
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
