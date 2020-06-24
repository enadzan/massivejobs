using System;
using System.Threading;
using System.Collections.Generic;

namespace MassiveJobs.Core
{
    public class WorkerCoordinator: IDisposable
    {
        protected readonly ILogger Logger;
        protected readonly IJobPublisher JobPublisher;
        protected readonly List<IWorker> Workers;
        protected readonly object WorkersLock = new object();

        protected volatile IMessageBroker MessageBroker;

        private readonly Timer _reconnectTimer;
        private readonly MassiveJobsSettings _settings;

        public WorkerCoordinator(IJobPublisher jobPublisher, MassiveJobsSettings settings, ILogger logger = null)
        {
            _reconnectTimer = new Timer(Reconnect);
            _settings = settings;
            Logger = logger ?? settings.LoggerFactory.SafeCreateLogger<WorkerCoordinator>();

            JobPublisher = jobPublisher;
            Workers = new List<IWorker>();
        }

        public virtual void Dispose()
        {
            lock (WorkersLock)
            {
                StopWorkers();
                DisposeBroker();
            }
        }

        public void StartWorkers()
        {
            lock (WorkersLock)
            {
                if (Workers.Count > 0) return;
                try
                {
                    EnsureBrokerExists();
                    CreateWorkers();

                    foreach (var worker in Workers)
                    {
                        worker.Start();
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, "Failed starting workers");
                    StopWorkers();

                    _reconnectTimer.Change(5 * 1000, Timeout.Infinite);
                }
            }
        }

        public void StopWorkers()
        {
            lock (WorkersLock)
            {
                try
                {
                    foreach (var worker in Workers)
                    {
                        try
                        {
                            worker.Stop();
                            worker.SafeDispose();
                        }
                        catch (Exception ex)
                        {
                            Logger.LogError(ex, $"Stopping a worker failed");
                        }
                    }

                    Workers.Clear();
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, "Stopping workers failed");
                }
            }
        }

        protected void OnWorkerError(Exception _)
        {
            try
            {
                StopWorkers();
            }
            finally
            {
                _reconnectTimer.Change(5 * 1000, Timeout.Infinite);
            }
        }

        protected void EnsureBrokerExists()
        {
            if (MessageBroker != null) return;

            MessageBroker = _settings.MessageBrokerFactory.CreateMessageBroker();
            MessageBroker.Disconnected += MessageBrokerDisconnected;
            MessageBroker.DeclareTopology();

            OnMessageBrokerCreated();
        }

        protected virtual void OnMessageBrokerCreated()
        {
        }

        protected virtual void OnMessageBrokerDisposing()
        {
        }

        protected virtual void CreateWorkers()
        {
            for (var i = 0; i < _settings.ImmediateWorkersCount; i++)
            {
                var queueName = string.Format(_settings.ImmediateQueueNameTemplate, i);

                var worker = new WorkerImmediate(
                    MessageBroker,
                    queueName,
                    _settings.ImmediateWorkersBatchSize,
                    JobPublisher,
                    _settings.JobRunner,
                    _settings.Serializer,
                    _settings.TypeProvider,
                    _settings.ServiceScopeFactory,
                    _settings.LoggerFactory.SafeCreateLogger<WorkerImmediate>()
                    );

                worker.Error += OnWorkerError;
                Workers.Add(worker);
            }

            for (var i = 0; i < _settings.ScheduledWorkersCount; i++)
            {
                var queueName = string.Format(_settings.ScheduledQueueNameTemplate, i);

                var worker = new WorkerScheduled(
                    MessageBroker,
                    queueName,
                    _settings.ScheduledWorkersBatchSize,
                    JobPublisher,
                    _settings.JobRunner,
                    _settings.Serializer,
                    _settings.TypeProvider,
                    _settings.ServiceScopeFactory,
                    _settings.LoggerFactory.SafeCreateLogger<WorkerScheduled>()
                    );

                worker.Error += OnWorkerError;
                Workers.Add(worker);
            }

            var periodicWorker = new WorkerScheduled(
               MessageBroker,
               _settings.PeriodicQueueName,
               _settings.PeriodicWorkersBatchSize,
               JobPublisher,
               _settings.JobRunner,
               _settings.Serializer,
               _settings.TypeProvider,
               _settings.ServiceScopeFactory,
               _settings.LoggerFactory.SafeCreateLogger<WorkerScheduled>()
               );

            periodicWorker.Error += OnWorkerError;
            Workers.Add(periodicWorker);

            var errorWorker = new WorkerScheduled(
                MessageBroker,
                _settings.ErrorQueueName,
                _settings.ScheduledWorkersBatchSize,
                JobPublisher,
                _settings.JobRunner,
                _settings.Serializer,
                _settings.TypeProvider,
                _settings.ServiceScopeFactory,
                _settings.LoggerFactory.SafeCreateLogger<WorkerScheduled>()
                );

            errorWorker.Error += OnWorkerError;
            Workers.Add(errorWorker);
        }

        private void Reconnect(object state)
        {
            StartWorkers();
        }

        /// <summary>
        /// This can be called from a different thread so must be in lock
        /// </summary>
        /// <param name="sender"></param>
        private void MessageBrokerDisconnected(IMessageBroker sender)
        {
            lock (WorkersLock)
            {
                try
                {
                    StopWorkers();
                    DisposeBroker();
                }
                finally
                {
                    _reconnectTimer.Change(5 * 1000, Timeout.Infinite);
                }
            }
        }

        private void DisposeBroker()
        {
            if (MessageBroker == null) return;

            try
            {
                OnMessageBrokerDisposing();
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error in call to OnMessageBrokerDisposing");
            }

            MessageBroker.SafeDispose();
            MessageBroker = null;
        }
    }
}
