using System;
using System.Collections.Generic;
using System.IO;

using RabbitMQ.Client;
using RabbitMQ.Client.Events;

using MassiveJobs.Core;

namespace MassiveJobs.RabbitMqBroker
{
    public abstract class RabbitMqMessageBroker
    {
        protected volatile IConnection Connection;
        protected volatile ModelPool ModelPool;

        protected readonly IJobLogger<RabbitMqMessageBroker> Logger;
        protected readonly string ExchangeName;

        private readonly object _connectionLock = new object();
        private readonly ConnectionFactory _connectionFactory;
        private readonly RabbitMqSettings _rabbitMqSettings;
        private readonly MassiveJobsSettings _massiveJobsSettings;

        protected RabbitMqMessageBroker(RabbitMqSettings rabbitMqSettings, MassiveJobsSettings massiveJobsSettings, bool automaticRecoveryEnabled, 
            IJobLogger<RabbitMqMessageBroker> logger)
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

                ModelPool.SafeDispose(Logger);
                ModelPool = null;

                Connection.CallbackException -= ConnectionOnCallbackException;
                Connection.ConnectionBlocked -= ConnectionOnConnectionBlocked;
                Connection.ConnectionUnblocked -= ConnectionOnConnectionUnblocked;
                Connection.ConnectionShutdown -= ConnectionOnConnectionShutdown;

                Connection.SafeClose(Logger);
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
                    Connection = _connectionFactory.CreateConnection(_rabbitMqSettings.HostNames, $"MassiveJobs.NET/{GetEntryFileName()}");

                    Connection.CallbackException += ConnectionOnCallbackException;
                    Connection.ConnectionBlocked += ConnectionOnConnectionBlocked;
                    Connection.ConnectionUnblocked += ConnectionOnConnectionUnblocked;
                    Connection.ConnectionShutdown += ConnectionOnConnectionShutdown;

                    ModelPool = new ModelPool(Connection, 2, Logger);

                    var model = ModelPool.Get();
                    try
                    {
                        DeclareTopology(model.Model);
                    }
                    finally
                    {
                        ModelPool.Return(model);
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

        private void ConnectionOnConnectionShutdown(object sender, ShutdownEventArgs e)
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
        }

        protected virtual void OnDisconnected()
        {
        }

        private void ConnectionOnConnectionUnblocked(object sender, EventArgs e)
        {
            Logger.LogInformation("Connection unblocked");
        }

        private void ConnectionOnConnectionBlocked(object sender, ConnectionBlockedEventArgs e)
        {
            Logger.LogError($"Connection blocked: {e.Reason}");
        }

        private void ConnectionOnCallbackException(object sender, CallbackExceptionEventArgs e)
        {
            Logger.LogError(e.Exception, "Callback exception");
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

        protected virtual void DeclareTopology(IModel model)
        {
            model.ExchangeDeclare(ExchangeName, ExchangeType.Direct, true);

            for (var i = 0; i < _massiveJobsSettings.ScheduledWorkersCount; i++)
            {
                var queueName = string.Format(_massiveJobsSettings.ScheduledQueueNameTemplate, i);
                DeclareAndBindQueue(model, ExchangeName, queueName, _massiveJobsSettings.MaxQueueLength);
            }

            for (var i = 0; i < _massiveJobsSettings.ImmediateWorkersCount; i++)
            {
                var queueName = string.Format(_massiveJobsSettings.ImmediateQueueNameTemplate, i);
                DeclareAndBindQueue(model, ExchangeName, queueName, _massiveJobsSettings.MaxQueueLength);
            }

            for (var i = 0; i < _massiveJobsSettings.LongRunningWorkersCount; i++)
            {
                var queueName = string.Format(_massiveJobsSettings.LongRunningQueueNameTemplate, i);
                DeclareAndBindQueue(model, ExchangeName, queueName, _massiveJobsSettings.MaxQueueLength);
            }

            for (var i = 0; i < _massiveJobsSettings.PeriodicWorkersCount; i++)
            {
                var queueName = string.Format(_massiveJobsSettings.PeriodicQueueNameTemplate, i);
                DeclareAndBindQueue(model, ExchangeName, queueName, _massiveJobsSettings.MaxQueueLength, false, true);
            }

            DeclareAndBindQueue(model, ExchangeName, _massiveJobsSettings.ErrorQueueName, QueueLength.NoLimit, isLazy: true);
            DeclareAndBindQueue(model, ExchangeName, _massiveJobsSettings.FailedQueueName, QueueLength.NoLimit, isLazy: true);
            DeclareAndBindQueue(model, ExchangeName, _massiveJobsSettings.StatsQueueName, 1000, true, true);
        }

        protected static void DeclareAndBindQueue(IModel model, string exchangeName, string queueName, int maxLength, bool dropHeadOnOverflow = false,
            bool singleActiveConsumer = false, bool persistent = true, string routingKey = null, bool isLazy = false)
        {
            DeclareQueue(model, queueName, maxLength, dropHeadOnOverflow, singleActiveConsumer, persistent, isLazy);
            BindQueue(model, queueName, exchangeName, routingKey ?? queueName);
        }

        protected static void DeclareQueue(IModel model, string queueName, int maxLength, bool dropHeadOnOverflow = false,
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

            model.QueueDeclare(queueName, persistent, false, !persistent, queueArguments);
        }

        protected static void BindQueue(IModel model, string queueName, string exchangeName, string routingKey)
        {
             model.QueueBind(queueName, exchangeName, routingKey);
        }
    }
}
