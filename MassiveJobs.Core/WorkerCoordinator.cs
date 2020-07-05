using System;
using System.Threading;
using System.Collections.Generic;

namespace MassiveJobs.Core
{
    public class WorkerCoordinator : IWorkerCoordinator
    {
        protected readonly ILogger Logger;
        protected readonly List<IWorker> Workers;
        protected readonly object WorkersLock = new object();

        protected readonly IMessageConsumer MessageConsumer;
        protected readonly ILoggerFactory LoggerFactory;
        protected readonly IServiceScopeFactory ServiceScopeFactory;

        private readonly Timer _reconnectTimer;
        private readonly MassiveJobsSettings _settings;

        public WorkerCoordinator(
            MassiveJobsSettings settings, 
            IMessageConsumer messageConsumer, 
            IServiceScopeFactory serviceScopeFactory, 
            ILoggerFactory loggerFactory, 
            ILogger logger = null)
        {
            _settings = settings;
            _reconnectTimer = new Timer(Reconnect);

            Workers = new List<IWorker>();
            Logger = logger ?? loggerFactory.SafeCreateLogger<WorkerCoordinator>();

            ServiceScopeFactory = serviceScopeFactory;
            LoggerFactory = loggerFactory;

            MessageConsumer = messageConsumer;
            MessageConsumer.Disconnected += MessageBrokerDisconnected;
        }

        public virtual void Dispose()
        {
            lock (WorkersLock)
            {
                StopJobWorkers();
            }
        }

        public void StartJobWorkers()
        {
            lock (WorkersLock)
            {
                if (Workers.Count > 0) return;
                try
                {
                    CreateWorkers();

                    foreach (var worker in Workers)
                    {
                        worker.Start();
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, "Failed starting workers");
                    StopJobWorkers();

                    _reconnectTimer.Change(5 * 1000, Timeout.Infinite);
                }
            }
        }

        public void StopJobWorkers()
        {
            lock (WorkersLock)
            {
                try
                {
                    foreach (var worker in Workers)
                    {
                        try
                        {
                            worker.BeginStop();
                        }
                        catch (Exception ex)
                        {
                            Logger.LogError(ex, $"Begin stop for a worker failed");
                        }
                    }

                    foreach (var worker in Workers)
                    {
                        try
                        {
                            worker.WaitUntilStopped();
                            worker.SafeDispose(Logger);
                        }
                        catch (Exception ex)
                        {
                            Logger.LogError(ex, $"Stopping a worker failed");
                        }
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, "Stopping workers failed");
                }

                Workers.Clear();
            }
        }

        protected void OnWorkerError(Exception _)
        {
            try
            {
                StopJobWorkers();
            }
            finally
            {
                _reconnectTimer.Change(5 * 1000, Timeout.Infinite);
            }
        }

        protected virtual void CreateWorkers()
        {
            MessageConsumer.Connect();

            for (var i = 0; i < _settings.ImmediateWorkersCount; i++)
            {
                var queueName = string.Format(_settings.ImmediateQueueNameTemplate, i);

                var worker = new WorkerImmediate(
                    queueName,
                    _settings.ImmediateWorkersBatchSize,
                    MessageConsumer,
                    ServiceScopeFactory,
                    LoggerFactory.SafeCreateLogger<WorkerImmediate>()
                    );

                worker.Error += OnWorkerError;
                Workers.Add(worker);
            }

            for (var i = 0; i < _settings.ScheduledWorkersCount; i++)
            {
                var queueName = string.Format(_settings.ScheduledQueueNameTemplate, i);

                var worker = new WorkerScheduled(
                    queueName,
                    _settings.ScheduledWorkersBatchSize,
                    MessageConsumer,
                    ServiceScopeFactory,
                    LoggerFactory.SafeCreateLogger<WorkerScheduled>()
                    );

                worker.Error += OnWorkerError;
                Workers.Add(worker);
            }

            for (var i = 0; i < _settings.PeriodicWorkersCount; i++)
            {
                var queueName = string.Format(_settings.PeriodicQueueNameTemplate, i);

                var periodicWorker = new WorkerScheduled(
                    queueName,
                    _settings.PeriodicWorkersBatchSize,
                    MessageConsumer,
                    ServiceScopeFactory,
                    LoggerFactory.SafeCreateLogger<WorkerScheduled>()
                    );

                periodicWorker.Error += OnWorkerError;
                Workers.Add(periodicWorker);
            }

            var errorWorker = new WorkerScheduled(
                _settings.ErrorQueueName,
                _settings.ScheduledWorkersBatchSize,
                MessageConsumer,
                ServiceScopeFactory,
                LoggerFactory.SafeCreateLogger<WorkerScheduled>()
                );

            errorWorker.Error += OnWorkerError;
            Workers.Add(errorWorker);
        }

        private void Reconnect(object state)
        {
            Logger.LogDebug("Reconnecting");
            StartJobWorkers();
        }

        /// <summary>
        /// This can be called from a different thread so must be in lock
        /// </summary>
        /// <param name="sender"></param>
        private void MessageBrokerDisconnected(IMessageConsumer sender)
        {
            Logger.LogWarning("Message broker disconnected... stopping workers");

            lock (WorkersLock)
            {
                try
                {
                    StopJobWorkers();
                }
                finally
                {
                    Logger.LogDebug("Reconnect timer started");
                    _reconnectTimer.Change(5 * 1000, Timeout.Infinite);
                }
            }

            Logger.LogWarning("Message broker disconnected... stopped workers");
        }
    }
}
