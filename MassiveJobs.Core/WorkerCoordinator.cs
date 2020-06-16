using Microsoft.Extensions.Logging;
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
            StopWorkers();
            DisposeBroker();
        }

        public void StartWorkers()
        {
            try
            {
                lock (WorkersLock)
                {
                    EnsureBrokerExists();
                    CreateWorkers();

                    foreach (var worker in Workers)
                    {
                        worker.Start();
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Failed starting workers");

                StopWorkers();
                _reconnectTimer.Change(5 * 1000, Timeout.Infinite);
            }
        }

        public void StopWorkers()
        {
            try
            {
                lock (WorkersLock)
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
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Stopping workers failed");
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
            MessageBroker.DeclareTopology();

            OnMessageBrokerCreated();
        }

        protected virtual void OnMessageBrokerCreated()
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
                    _settings.ConsumeBatchSize,
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
                    _settings.ConsumeBatchSize,
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

            var errorWorker = new WorkerScheduled(
                MessageBroker,
                _settings.ErrorQueueName,
                _settings.ConsumeBatchSize,
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

        private void DisposeBroker()
        {
            lock (WorkersLock)
            {
                MessageBroker.SafeDispose();
            }
        }
    }
}
