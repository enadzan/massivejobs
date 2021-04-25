using System;
using System.Linq;
using System.Text;
using System.Threading;

using Microsoft.EntityFrameworkCore;

using MassiveJobs.Core;
using MassiveJobs.Core.DependencyInjection;

namespace MassiveJobs.SqlServerBroker
{
    public class SqlServerMessageReceiver<TDbContext> : IMessageReceiver where TDbContext: DbContext
    {
        private const int BatchSize = 100;

        private readonly string _queueName;
        private readonly bool _singleActiveConsumer;
        private readonly IJobServiceScopeFactory _scopeFactory;
        private readonly IJobLogger<SqlServerMessageReceiver<TDbContext>> _logger;
        private readonly Timer _timer;
        private readonly string _instanceName;

        private bool _initialized;
        private volatile bool _isDisposed;

        public event MessageReceivedHandler MessageReceived;

        public SqlServerMessageReceiver(string queueName, bool singleActiveConsumer, IJobServiceScopeFactory scopeFactory,
            IJobLogger<SqlServerMessageReceiver<TDbContext>> logger)
        {
            _queueName = queueName;
            _singleActiveConsumer = singleActiveConsumer;
            _scopeFactory = scopeFactory;
            _logger = logger;
            _instanceName = $"{Environment.MachineName}/{Guid.NewGuid()}";

            _timer = new Timer(TimerCallback);
        }

        public void AckBatchProcessed(ulong lastDeliveryTag)
        {
        }

        public void AckBatchMessageProcessed(IJobServiceScope scope, ulong deliveryTag)
        {
            AckMessageProcessed(scope, deliveryTag);
        }

        public void AckMessageProcessed(IJobServiceScope scope, ulong deliveryTag)
        {
            var dbContext = scope.GetRequiredService<TDbContext>();

            var utcNow = DateTime.UtcNow;

            var affectedCount = dbContext.Database.ExecuteSqlInterpolated($@"
UPDATE massive_jobs.message_queue 
SET processing_end_utc = {utcNow} 
WHERE processing_end_utc IS NULL
    AND processing_instance = {_instanceName}
");

            if (affectedCount != 1) throw new Exception($"Ack failed. Expected 1 row, but instead, {affectedCount} rows were affected");
        }

        public void Dispose()
        {
            lock (_timer) 
             { 
                _timer.Change(Timeout.Infinite, Timeout.Infinite);
                _timer.SafeDispose(_logger);
                _isDisposed = true;
            }
        }

        public void Start()
        {
            _timer.Change(2000, Timeout.Infinite);
        }

        private void TimerCallback(object state)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();

                var dbContext = scope.GetRequiredService<TDbContext>();

                var utcNow = DateTime.UtcNow;

                TryInitialize(dbContext);

                if (TryGetLock(dbContext, utcNow))
                { 
                    ProcessMessages(dbContext, utcNow);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in TimerCallback");
            }
            finally
            {
                lock (_timer) 
                {
                    if (!_isDisposed) _timer.Change(2000, Timeout.Infinite);
                }
            }
        }

        private void TryInitialize(TDbContext dbContext)
        {
            if (_initialized) return;

            if (_singleActiveConsumer)
            {
                var lockExist = dbContext.Set<SingleConsumerLock>().Find(_queueName) != null;

                if (!lockExist)
                {
                    try
                    {
                        dbContext.Set<SingleConsumerLock>().Add(new SingleConsumerLock
                        {
                            RoutingKey = _queueName
                        });

                        dbContext.SaveChanges();
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Could not create single consumer lock");
                    }
                }
            }

            _initialized = true;
        }

        private bool TryGetLock(TDbContext dbContext, DateTime utcNow)
        {
            if (!_singleActiveConsumer) return true;

            var rowsAffected = dbContext.Database.ExecuteSqlInterpolated($@"
UPDATE massive_jobs.single_consumer_lock 
SET lock_keepalive_utc = {utcNow}, instance_name = {_instanceName}
WHERE routing_key = {_queueName}
    AND (lock_keepalive_utc IS NULL OR lock_keepalive_utc < {utcNow.AddSeconds(-20)} OR instance_name = {_instanceName})
");
            return rowsAffected > 0;
        }

        private void ProcessMessages(TDbContext dbContext, DateTime utcNow)
        {
            var unconfirmedCount = dbContext.Database.ExecuteSqlInterpolated($@"
UPDATE massive_jobs.message_queue 
SET processing_keepalive_utc = {utcNow} 
WHERE processing_end_utc IS NULL
    AND processing_instance = {_instanceName}
");
            var remaining = BatchSize - unconfirmedCount;
            if (remaining <= 0) return;

            var messages = dbContext.Set<MessageQueue>().FromSqlInterpolated($@"
UPDATE TOP ({remaining}) massive_jobs.message_queue
SET processing_start_utc = {utcNow}, processing_keepalive_utc = {utcNow}, processing_instance = {_instanceName}
OUTPUT inserted.*
WHERE processing_end_utc IS NULL
    AND routing_key = {_queueName}
    AND (processing_keepalive_utc IS NULL OR processing_keepalive_utc < {utcNow.AddSeconds(-20)})
")
                .ToList();

            foreach (var msg in messages)
            {
                OnMessageReceived(msg);
            }
        }

        private void OnMessageReceived(MessageQueue msg)
        {
            try
            {
                if (MessageReceived == null) return;

                var deliveryTag = BitConverter.ToUInt64(BitConverter.GetBytes(msg.Id));

                var raw = new RawMessage
                {
                    TypeTag = msg.MessageType,
                    IsPersistent = true,
                    Body = Encoding.UTF8.GetBytes(msg.MessageData),
                    DeliveryTag = deliveryTag
                };

                MessageReceived(this, raw);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error in {nameof(OnMessageReceived)}");
            }
        }
    }
}
