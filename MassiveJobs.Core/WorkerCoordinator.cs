﻿using System;
using System.Threading;
using System.Collections.Generic;

using Microsoft.Extensions.Logging;

namespace MassiveJobs.Core
{
    public class WorkerCoordinator : IWorkerCoordinator
    {
        protected readonly ILogger<WorkerCoordinator> Logger;
        protected readonly List<IWorker> Workers;
        protected readonly object WorkersLock = new object();

        protected readonly IMessageConsumer MessageConsumer;
        protected readonly IServiceProvider ServiceProvider;

        private readonly Timer _reconnectTimer;
        private readonly MassiveJobsSettings _settings;

        private readonly int[] _reconnectTimes = {1, 2, 5, 10, 30, 60, 120};
        private int _reconnectTimeIndex;

        public WorkerCoordinator(IMessageConsumer messageConsumser, MassiveJobsSettings settings, 
            IServiceProvider serviceProvider, ILogger<WorkerCoordinator> logger)
        {
            _settings = settings;

            ServiceProvider = serviceProvider;
            Logger = logger;

            _reconnectTimer = new Timer(Reconnect);

            Workers = new List<IWorker>();

            MessageConsumer = messageConsumser;
            MessageConsumer.Disconnected += MessageBrokerDisconnected;
        }

        public virtual void Dispose()
        {
            StopJobWorkers(false);
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

                    // reset reconnect time index upon first successful connect
                    _reconnectTimeIndex = 0;
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, "Failed starting workers");
                    StopJobWorkers(true);
                }
            }
        }

        public void StopJobWorkers()
        {
            StopJobWorkers(false);
        }

        private void StopJobWorkers(bool reconnect)
        {
            lock (WorkersLock)
            {
                _reconnectTimer.Change(Timeout.Infinite, Timeout.Infinite);

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
                            Logger.LogError(ex, "Begin stop for a worker failed");
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
                            Logger.LogError(ex, "Stopping a worker failed");
                        }
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, "Stopping workers failed");
                }

                Workers.Clear();

                if (reconnect)
                {
                    if (_reconnectTimeIndex < _reconnectTimes.Length - 1)
                    {
                        _reconnectTimeIndex++;
                    }

                    _reconnectTimer.Change(_reconnectTimes[_reconnectTimeIndex] * 1000, Timeout.Infinite);
                    Logger.LogDebug("Reconnect timer started");
                }
            }
        }

        protected void OnWorkerError(Exception _)
        {
            StopJobWorkers(true);
        }

        /// <summary>
        /// This method should never be called by a sub-class.
        /// This method is protected only to allow inheritors to create additional workers by overriding this method.
        /// </summary>
        protected virtual void CreateWorkers()
        {
            // ReSharper disable InconsistentlySynchronizedField
            MessageConsumer.Connect();

            var errorWorker = new WorkerScheduled(WorkerType.Error, 0, ServiceProvider);
            errorWorker.Error += OnWorkerError;

            Workers.Add(errorWorker);

            for (var i = 0; i < _settings.ScheduledWorkersCount; i++)
            {
                //var queueName = string.Format(_settings.ScheduledQueueNameTemplate, i);

                var worker = new WorkerScheduled(WorkerType.Scheduled, i, ServiceProvider);
                worker.Error += OnWorkerError;

                Workers.Add(worker);
            }

            for (var i = 0; i < _settings.PeriodicWorkersCount; i++)
            {
                //var queueName = string.Format(_settings.PeriodicQueueNameTemplate, i);

                var worker = new WorkerScheduled(WorkerType.Periodic, i, ServiceProvider);
                worker.Error += OnWorkerError;

                Workers.Add(worker);
            }

            for (var i = 0; i < _settings.ImmediateWorkersCount; i++)
            {
                //var queueName = string.Format(_settings.ImmediateQueueNameTemplate, i);

                var worker = new WorkerImmediate(WorkerType.Immedate, i, ServiceProvider);
                worker.Error += OnWorkerError;

                Workers.Add(worker);
            }

            for (var i = 0; i < _settings.LongRunningWorkersCount; i++)
            {
                //var queueName = string.Format(_settings.LongRunningQueueNameTemplate, i);

                var worker = new WorkerImmediate(WorkerType.LongRunning, i, ServiceProvider);
                worker.Error += OnWorkerError;

                Workers.Add(worker);
            }

            // ReSharper restore InconsistentlySynchronizedField
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

            StopJobWorkers(true);

            Logger.LogWarning("Message broker disconnected... stopped workers");
        }
    }
}
