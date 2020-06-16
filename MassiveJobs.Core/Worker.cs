using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;

namespace MassiveJobs.Core
{
    public abstract class Worker : IWorker
    {
        private readonly int _batchSize;

        private readonly AutoResetEvent _stoppingSignal = new AutoResetEvent(false);

        private readonly object _startStopLock = new object();

        private volatile Thread _workerThread;
        private volatile CancellationTokenSource _cancellationTokenSource;
        private volatile IMessageConsumer _messageConsumer;

        public event Action<Exception> Error;

        protected readonly IMessageBroker MessageBroker;
        protected readonly string QueueName;
        protected readonly IJobPublisher JobPublisher;
        protected readonly IJobRunner JobRunner;
        protected readonly IJobSerializer Serializer;
        protected readonly IJobTypeProvider TypeProvider;
        protected readonly IServiceScopeFactory ServiceScopeFactory;
        protected readonly ILogger Logger;
       
        protected readonly ConcurrentQueue<RawMessage> Messages;
        protected readonly AutoResetEvent MessageArrivedSignal = new AutoResetEvent(false);

        protected CancellationToken CancellationToken;

        protected abstract void ProcessMessageBatch(List<RawMessage> messages, IServiceScope serviceScope);

        protected Worker(
            IMessageBroker messageBroker,
            string queueName,
            int batchSize,
            IJobPublisher jobPublisher,
            IJobRunner jobRunner,
            IJobSerializer serializer,
            IJobTypeProvider typeProvider,
            IServiceScopeFactory scopeFactory,
            ILogger logger)
        {
            _batchSize = batchSize;

            MessageBroker = messageBroker;
            QueueName = queueName;
            JobPublisher = jobPublisher;
            JobRunner = jobRunner;
            Serializer = serializer;
            TypeProvider = typeProvider;
            ServiceScopeFactory = scopeFactory;
            Logger = logger ?? new DefaultLogger<Worker>();

            Messages = new ConcurrentQueue<RawMessage>();
        }

        public virtual void Dispose()
        {
            Stop();
        }

        public void Start()
        {
            lock (_startStopLock)
            {
                if (_workerThread != null)
                {
                    Logger.LogDebug($"Worker {QueueName} already running");
                    return;
                }

                Logger.LogInformation($"Starting worker {QueueName}");

                EnsureConsumerExists();

                _cancellationTokenSource = new CancellationTokenSource();
                CancellationToken = _cancellationTokenSource.Token;

                _workerThread = new Thread(WorkerFunction) { IsBackground = true };
                _workerThread.Start();

                OnStarted();

                Logger.LogInformation($"Worker {QueueName} started");
            }
        }

        public void Stop()
        {
            lock (_startStopLock)
            {
                if (_workerThread == null)
                {
                    Logger.LogDebug($"Worker {QueueName} already stopped");
                    return;
                }

                DisposeConsumer();
                
                _cancellationTokenSource.Cancel();
                MessageArrivedSignal.Set(); //to speed up the shutdown

                Logger.LogWarning($"Stopping worker {QueueName}");

                _stoppingSignal.WaitOne();

                _cancellationTokenSource.SafeDispose();
                _cancellationTokenSource = null;
                _workerThread = null;

                // empty the queue of messages
                while (Messages.TryDequeue(out _)) ;

                OnStopped();

                Logger.LogWarning($"Worker stopped {QueueName}");
            }
        }

        protected virtual void OnStarted()
        {
        }

        protected virtual void OnStopped()
        {
        }

        protected void EnsureConsumerExists()
        {
            // this may be called from subclass
            lock (_startStopLock)
            {
                if (_messageConsumer != null) return;

                _messageConsumer = MessageBroker.CreateConsumer(QueueName);
                _messageConsumer.MessageReceived += ConsumerOnMessageReceived;
            }
        }

        protected void DisposeConsumer()
        {
            lock (_startStopLock)
            {
                if (_messageConsumer == null) return;

                _messageConsumer.MessageReceived -= ConsumerOnMessageReceived;
                _messageConsumer.SafeDispose();
                _messageConsumer = null;

                // empty the queue of messages
                while (Messages.TryDequeue(out _)) ;
            }
        }

        protected void OnError(Exception ex)
        {
            try
            {
                Logger.LogError(ex, "Error while processing jobs");
                Error?.Invoke(ex);
            }
            catch (Exception eventEx)
            {
                Logger.LogError(eventEx, "Exception raised while processing Error event handler");
            }
        }

        protected bool TryDeserializeJob(RawMessage rawMessage, out JobInfo job)
        {
            job = null;

            var argsTag = rawMessage.TypeTag;
            if (argsTag == null || argsTag == string.Empty) return false;

            job = Serializer.Deserialize(rawMessage.Body, argsTag, TypeProvider);
            return job != null;
        }

        private void WorkerFunction()
        {
            Exception exceptionRaised = null;

            try
            {
                while (!_cancellationTokenSource.IsCancellationRequested)
                {
                    MessageArrivedSignal.WaitOne();

                    while (!_cancellationTokenSource.IsCancellationRequested && Messages.Count > 0)
                    {
                        var batch = new List<RawMessage>();

                        while (!_cancellationTokenSource.IsCancellationRequested && batch.Count < _batchSize && Messages.TryDequeue(out var rawMessage))
                        {
                            batch.Add(rawMessage);
                        }

                        if (batch.Count > 0)
                        {
                            var serviceScope = ServiceScopeFactory.SafeCreateScope();
                            try
                            {
                                ProcessMessageBatch(batch, serviceScope);
                            }
                            finally
                            {
                                serviceScope.SafeDispose();
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                exceptionRaised = ex;
            }

            _stoppingSignal.Set();

            if (exceptionRaised != null)
            {
                OnError(exceptionRaised);
            }
        }

        protected void OnBatchProcessed(ulong lastDeliveryTag)
        {
            _messageConsumer.AckBatchProcessed(lastDeliveryTag);
        }

        protected void OnMessageProcessed(ulong deliveryTag)
        {
            _messageConsumer.AckMessageProcessed(deliveryTag);
        }

        private void ConsumerOnMessageReceived(IMessageConsumer sender, RawMessage message)
        {
            // constructing string is expensive and this is hot path
            if (Logger.IsEnabled(LogLevel.Trace))
            {
                Logger.LogTrace($"Message received on worker {QueueName}");
            }

            Messages.Enqueue(message);

            MessageArrivedSignal.Set();
        }
    }
}
