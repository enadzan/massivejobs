using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging;

using RabbitMQ.Client;
using RabbitMQ.Client.Events;

using MassiveJobs.Core;

namespace MassiveJobs.RabbitMqBroker
{
    public abstract class RabbitMqMessageBroker
    {
        protected volatile IConnection Connection;
        protected volatile ChannelPool ChannelPool;

        protected readonly ILogger<RabbitMqMessageBroker> Logger;
        protected readonly string ExchangeName;

        private readonly object _connectionLock = new object();
        private readonly ConnectionFactory _connectionFactory;
        private readonly RabbitMqSettings _rabbitMqSettings;
        private readonly MassiveJobsSettings _massiveJobsSettings;

        protected RabbitMqMessageBroker(RabbitMqSettings rabbitMqSettings, MassiveJobsSettings massiveJobsSettings, bool automaticRecoveryEnabled, 
            ILogger<RabbitMqMessageBroker> logger)
        {
            Logger = logger;
            ExchangeName = $"{massiveJobsSettings.NamePrefix}massivejobs.direct";

            _rabbitMqSettings = rabbitMqSettings;
            _massiveJobsSettings = massiveJobsSettings;

            _connectionFactory = new ConnectionFactory
            {
                Port = rabbitMqSettings.Port,
                UserName = rabbitMqSettings.Username,
                Password = rabbitMqSettings.Password,
                VirtualHost = rabbitMqSettings.VirtualHost,
                AutomaticRecoveryEnabled = automaticRecoveryEnabled,
                TopologyRecoveryEnabled = automaticRecoveryEnabled,
                RequestedHeartbeat = TimeSpan.FromSeconds(10),
                Ssl =
                {
                    Enabled = rabbitMqSettings.SslEnabled,
                    ServerName = rabbitMqSettings.SslServerName,
                    CertPath = rabbitMqSettings.SslClientCertPath,
                    CertPassphrase = rabbitMqSettings.SslClientCertPassPhrase
                }
            };
        }

        public virtual void Dispose()
        {
            CloseConnection();
        }

        protected void CloseConnection()
        {
            lock (_connectionLock)
            {
                if (Connection == null) return;

                ChannelPool.SafeDispose(Logger);
                ChannelPool = null;

                Connection.CallbackExceptionAsync -= ConnectionOnCallbackException;
                Connection.ConnectionBlockedAsync -= ConnectionOnConnectionBlocked;
                Connection.ConnectionUnblockedAsync -= ConnectionOnConnectionUnblocked;
                Connection.ConnectionShutdownAsync -= ConnectionOnConnectionShutdown;

                Connection.SafeCloseAsync(Logger).Wait();
                Connection = null;

                Logger.LogWarning("Connection closed and set to null");
            }
        }
        
        protected void EnsureConnectionExists()
        {
            lock (_connectionLock)
            {
                if (Connection != null) return;

                Logger.LogDebug("Connecting...");

                try
                {
                    Connection = _connectionFactory.CreateConnectionAsync(_rabbitMqSettings.HostNames, $"MassiveJobs.NET/{GetEntryFileName()}").Result;

                    Connection.CallbackExceptionAsync += ConnectionOnCallbackException;
                    Connection.ConnectionBlockedAsync += ConnectionOnConnectionBlocked;
                    Connection.ConnectionUnblockedAsync += ConnectionOnConnectionUnblocked;
                    Connection.ConnectionShutdownAsync += ConnectionOnConnectionShutdown;

                    ChannelPool = new ChannelPool(Connection, 2, _massiveJobsSettings.PublishBatchSize, Logger);

                    var model = ChannelPool.Get();
                    try
                    {
                        DeclareTopology(model.Channel).Wait();
                    }
                    finally
                    {
                        ChannelPool.Return(model);
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, "Failed connection create");
                    throw;
                }
            
                Logger.LogWarning("Connected");
            }
        }

        private Task ConnectionOnConnectionShutdown(object sender, ShutdownEventArgs e)
        {
            Logger.LogError($"Connection shutdown: {e.Cause} / {e.ReplyCode} / {e.ReplyText} ");
            try
            {
                OnDisconnected();
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error in executing Disconnected event handler");
            }

            if (!_connectionFactory.AutomaticRecoveryEnabled)
            {
                CloseConnection();
            }

            return Task.CompletedTask;
        }

        protected virtual void OnDisconnected()
        {
        }

        private Task ConnectionOnConnectionUnblocked(object sender, AsyncEventArgs e)
        {
            Logger.LogInformation("Connection unblocked");
            return Task.CompletedTask;
        }

        private Task ConnectionOnConnectionBlocked(object sender, ConnectionBlockedEventArgs e)
        {
            Logger.LogError($"Connection blocked: {e.Reason}");
            return Task.CompletedTask;
        }

        private Task ConnectionOnCallbackException(object sender, CallbackExceptionEventArgs e)
        {
            Logger.LogError(e.Exception, "Callback exception");
            return Task.CompletedTask;
        }

        private string GetEntryFileName()
        {
            try
            {
                var location = System.Reflection.Assembly.GetEntryAssembly()?.Location ?? "unknown";
                return Path.GetFileName(location);
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "Failed retrieving entry file name");
                return "";
            }
        }

        protected virtual async Task DeclareTopology(IChannel channel)
        {
            await channel.ExchangeDeclareAsync(ExchangeName, ExchangeType.Direct, true).ConfigureAwait(false);

            for (var i = 0; i < _massiveJobsSettings.ScheduledWorkersCount; i++)
            {
                var queueName = string.Format(_massiveJobsSettings.ScheduledQueueNameTemplate, i);
                await DeclareAndBindQueue(channel, ExchangeName, queueName, _massiveJobsSettings.MaxQueueLength).ConfigureAwait(false);
            }

            for (var i = 0; i < _massiveJobsSettings.ImmediateWorkersCount; i++)
            {
                var queueName = string.Format(_massiveJobsSettings.ImmediateQueueNameTemplate, i);
                await DeclareAndBindQueue(channel, ExchangeName, queueName, _massiveJobsSettings.MaxQueueLength).ConfigureAwait(false);
            }

            for (var i = 0; i < _massiveJobsSettings.LongRunningWorkersCount; i++)
            {
                var queueName = string.Format(_massiveJobsSettings.LongRunningQueueNameTemplate, i);
                await DeclareAndBindQueue(channel, ExchangeName, queueName, _massiveJobsSettings.MaxQueueLength).ConfigureAwait(false);
            }

            for (var i = 0; i < _massiveJobsSettings.PeriodicWorkersCount; i++)
            {
                var queueName = string.Format(_massiveJobsSettings.PeriodicQueueNameTemplate, i);
                await DeclareAndBindQueue(channel, ExchangeName, queueName, _massiveJobsSettings.MaxQueueLength, false, true).ConfigureAwait(false);
            }

            await DeclareAndBindQueue(channel, ExchangeName, _massiveJobsSettings.ErrorQueueName, QueueLength.NoLimit, isLazy: true).ConfigureAwait(false);
            await DeclareAndBindQueue(channel, ExchangeName, _massiveJobsSettings.FailedQueueName, QueueLength.NoLimit, isLazy: true).ConfigureAwait(false);
            await DeclareAndBindQueue(channel, ExchangeName, _massiveJobsSettings.StatsQueueName, 1000, true, true).ConfigureAwait(false);
        }

        protected static async Task DeclareAndBindQueue(IChannel model, string exchangeName, string queueName, int maxLength, bool dropHeadOnOverflow = false,
            bool singleActiveConsumer = false, bool persistent = true, string routingKey = null, bool isLazy = false)
        {
            await DeclareQueue(model, queueName, maxLength, dropHeadOnOverflow, singleActiveConsumer, persistent, isLazy)
                .ConfigureAwait(false);

            await BindQueue(model, queueName, exchangeName, routingKey ?? queueName)
                .ConfigureAwait(false);
        }

        protected static async Task DeclareQueue(IChannel channel, string queueName, int maxLength, bool dropHeadOnOverflow = false,
            bool singleActiveConsumer = false, bool persistent = true, bool isLazy = false)
        {
            var queueArguments = new Dictionary<string, object>();

            if (maxLength > 0)
            {
                queueArguments.Add("x-max-length", maxLength);
                queueArguments.Add("x-overflow", dropHeadOnOverflow ? "drop-head" : "reject-publish");
            }

            if (singleActiveConsumer)
            {
                queueArguments.Add("x-single-active-consumer", true);
            }

            if (isLazy)
            {
                queueArguments.Add("x-queue-mode", "lazy");
            }

            await channel.QueueDeclareAsync(queueName, persistent, false, !persistent, queueArguments)
                .ConfigureAwait(false);
        }

        protected static async Task BindQueue(IChannel channel, string queueName, string exchangeName, string routingKey)
        {
             await channel.QueueBindAsync(queueName, exchangeName, routingKey)
                .ConfigureAwait(false);
        }
    }
}
