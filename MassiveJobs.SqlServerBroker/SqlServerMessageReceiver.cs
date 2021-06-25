using System;
using System.Text;
using System.Threading;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

using MassiveJobs.Core;
using MassiveJobs.Core.DependencyInjection;

namespace MassiveJobs.SqlServerBroker
{
    public class SqlServerMessageReceiver<TDbContext> : IMessageReceiver where TDbContext: DbContext
    {
        private const int BatchSize = 100;

        private readonly string _queueName;
        private readonly bool _singleActiveConsumer;
        private readonly ISqlDialect _sqlDialect;
        private readonly ITimeProvider _timeProvider;
        private readonly IJobServiceScopeFactory _scopeFactory;
        private readonly IJobServiceScope _scope;
        private readonly IJobLogger<SqlServerMessageReceiver<TDbContext>> _logger;
        private readonly ITimer _timer;
        private readonly string _instanceName;

        private bool _initialized;
        private volatile bool _isDisposed;

        public event MessageReceivedHandler MessageReceived;

        public SqlServerMessageReceiver(string queueName, bool singleActiveConsumer, IJobServiceScopeFactory scopeFactory, IJobLogger<SqlServerMessageReceiver<TDbContext>> logger)
        {
            _scopeFactory = scopeFactory;
            _scope = scopeFactory.CreateScope();

            _queueName = queueName;
            _singleActiveConsumer = singleActiveConsumer;
            _sqlDialect = _scope.GetRequiredService<ISqlDialect>();
            _timeProvider = _scope.GetRequiredService<ITimeProvider>();
            _logger = logger;
            _instanceName = $"{Environment.MachineName}/{Guid.NewGuid()}";

            _timer = _scope.GetRequiredService<ITimer>();
            _timer.TimeElapsed += TimerCallback;
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

            var affectedCount = _sqlDialect.MessageQueueAckProcessed(dbContext, _instanceName, _timeProvider.GetCurrentTimeUtc());

            if (affectedCount != 1) throw new Exception($"Ack failed. Expected 1 row, but instead, {affectedCount} rows were affected");
        }

        public void Dispose()
        {
            lock (_timer) 
            { 
                _timer.TimeElapsed -= TimerCallback;
                _timer.Change(Timeout.Infinite, Timeout.Infinite);

                _scope.SafeDispose(_logger);
                _isDisposed = true;
            }
        }

        public void Start()
        {
            _timer.Change(2000, Timeout.Infinite);
        }

        private void TimerCallback()
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();

                var dbContext = scope.GetRequiredService<TDbContext>();

                var utcNow = _timeProvider.GetCurrentTimeUtc();

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
                    if (_isDisposed)
                    {
                        try
                        {
                            if (_singleActiveConsumer)
                            {
                                using var scope = _scopeFactory.CreateScope();
                                var dbContext = scope.GetRequiredService<TDbContext>();

                                _sqlDialect.MessageQueueRelease(dbContext, _instanceName, _queueName);
                                _sqlDialect.SingleConsumerLockRelease(dbContext, _instanceName, _queueName);
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Failed releasing single consumer lock");
                        }
                    }
                    else
                    {
                        _timer.Change(2000, Timeout.Infinite);
                    }
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

            var rowsAffected = _sqlDialect.SingleConsumerLockUpdate(dbContext, _instanceName, _queueName, utcNow); 

            return rowsAffected > 0;
        }

        private void ProcessMessages(TDbContext dbContext, DateTime utcNow)
        {
            var unconfirmedCount = _sqlDialect.MessageQueueKeepalive(dbContext, _instanceName, utcNow);

            var remaining = BatchSize - unconfirmedCount;
            if (remaining <= 0) return;

            var messages = _sqlDialect.MessageQueueGetNextBatch(dbContext, _instanceName, _queueName, utcNow, remaining);

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
                    TypeTag = msg.ArgsType,
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

        public IBrokerTransaction BeginTransaction(IJobServiceScope scope)
        {
            var dbContext = scope.GetRequiredService<TDbContext>();
            var tx = dbContext.Database.BeginTransaction();

            return new BrokerTransaction(tx);
        }

        private class BrokerTransaction: IBrokerTransaction
        {
            private readonly IDbContextTransaction _tx;

            public BrokerTransaction(IDbContextTransaction tx)
            {
                _tx = tx;
            }

            public void Commit()
            {
                _tx.Commit();
            }

            public void Dispose()
            {
                _tx.Dispose();
            }

            public void Rollback()
            {
                _tx.Rollback();
            }
        }
    }
}
