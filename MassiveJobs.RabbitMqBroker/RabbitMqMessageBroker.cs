﻿using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Extensions.Logging;

using RabbitMQ.Client;
using RabbitMQ.Client.Events;

using MassiveJobs.Core;

namespace MassiveJobs.RabbitMqBroker
{
    class RabbitMqMessageBroker: IMessageBroker
    {
        protected volatile IConnection Connection;
        protected readonly ILogger Logger;

        private readonly object _connectionLock = new object();
        private readonly IConnectionFactory _connectionFactory;
        private readonly RabbitMqSettings _rabbitMqSettings;
        private readonly MassiveJobsSettings _massiveJobsSettings;

        public RabbitMqMessageBroker(RabbitMqSettings rabbitMqSettings, MassiveJobsSettings massiveJobsSettings, ILogger logger)
        {
            Logger = logger;

            _rabbitMqSettings = rabbitMqSettings;
            _massiveJobsSettings = massiveJobsSettings;

            _connectionFactory = new ConnectionFactory
            {
                Port = rabbitMqSettings.Port,
                UserName = rabbitMqSettings.Username,
                Password = rabbitMqSettings.Password,
                VirtualHost = rabbitMqSettings.VirtualHost,
                AutomaticRecoveryEnabled = true,
                TopologyRecoveryEnabled = true,
                RequestedHeartbeat = TimeSpan.FromSeconds(60),
                Ssl =
                {
                    Enabled = rabbitMqSettings.SslEnabled,
                    ServerName = rabbitMqSettings.SslServerName,
                    CertPath = rabbitMqSettings.SslClientCertPath,
                    CertPassphrase = rabbitMqSettings.SslClientCertPassphrase
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
                Connection.SafeClose();
                Connection.SafeDispose();
                Connection = null;
            }
        }
        
        protected void EnsureConnectionExists()
        {
            if (Connection != null) return;

            lock (_connectionLock)
            {
                if (Connection != null) return;

                Logger.LogDebug("Connecting");

                try
                {
                    Connection = _connectionFactory.CreateConnection(_rabbitMqSettings.HostNames, $"{GetEntryFileName()}/MassiveJobs::{GetType().Name}");
                    Connection.CallbackException += ConnectionOnCallbackException;
                    Connection.ConnectionBlocked += ConnectionOnConnectionBlocked;
                    Connection.ConnectionUnblocked += ConnectionOnConnectionUnblocked;
                    Connection.ConnectionShutdown += ConnectionOnConnectionShutdown;
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, "Failed connection create");
                    throw;
                }
            }

            Logger.LogInformation("Connected");
        }

        private void ConnectionOnConnectionShutdown(object sender, ShutdownEventArgs e)
        {
            Logger.LogError($"Connection shutdown: {e.Cause} / {e.ReplyCode} / {e.ReplyText} ");
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
                var location = System.Reflection.Assembly.GetEntryAssembly().Location;
                return Path.GetFileName(location);
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "Failed retreiving entry file name");
                return "";
            }
        }

        public IMessageConsumer CreateConsumer(string queueName)
        {
            EnsureConnectionExists();
            return new RabbitMqMessageConsumer(Connection, queueName, _massiveJobsSettings.ConsumeBatchSize);
        }

        public IMesagePublisher CreatePublisher()
        {
            EnsureConnectionExists();
            return new RabbitMqMessagePublisher(Connection);
        }

        public void DeclareTopology()
        {
            EnsureConnectionExists();

            using (var model = Connection.CreateModel())
            {
                model.ExchangeDeclare(_massiveJobsSettings.ExchangeName, ExchangeType.Direct, true);

                for (var i = 0; i < _massiveJobsSettings.ScheduledWorkersCount; i++)
                {
                    var queueName = string.Format(_massiveJobsSettings.ScheduledQueueNameTemplate, i);
                    DeclareAndBindQueue(model, _massiveJobsSettings.ExchangeName, queueName, _massiveJobsSettings.MaxQueueLength);
                }

                for (var i = 0; i < _massiveJobsSettings.ImmediateWorkersCount; i++)
                {
                    var queueName = string.Format(_massiveJobsSettings.ImmediateQueueNameTemplate, i);
                    DeclareAndBindQueue(model, _massiveJobsSettings.ExchangeName, queueName, _massiveJobsSettings.MaxQueueLength);
                }

                DeclareAndBindQueue(model, _massiveJobsSettings.ExchangeName, _massiveJobsSettings.ErrorQueueName, _massiveJobsSettings.MaxQueueLength);
                DeclareAndBindQueue(model, _massiveJobsSettings.ExchangeName, _massiveJobsSettings.FailedQueueName, _massiveJobsSettings.MaxQueueLength);
                DeclareAndBindQueue(model, _massiveJobsSettings.ExchangeName, _massiveJobsSettings.StatsQueueName, 1000, true, true);

                model.SafeClose();
            }
        }

        protected static void DeclareAndBindQueue(IModel model, string exchangeName, string queueName, int maxLength, bool dropHeadOnOverflow = false,
            bool singleActiveConsumer = false, bool persistent = true, string routingKey = null)
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

            model.QueueDeclare(queueName, persistent, false, !persistent, queueArguments);

            model.QueueBind(queueName, exchangeName, routingKey ?? queueName);
        }
    }
}
