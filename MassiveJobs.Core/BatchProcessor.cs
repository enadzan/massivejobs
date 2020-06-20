using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;

using Microsoft.Extensions.Logging;

namespace MassiveJobs.Core
{
    public abstract class BatchProcessor<TMessage>
    {
        private readonly int _batchSize;

        private readonly AutoResetEvent _stoppingSignal = new AutoResetEvent(false);
        private readonly object _startStopLock = new object();

        private readonly ConcurrentQueue<TMessage> _messages;
        private readonly AutoResetEvent _messageAddedSignal = new AutoResetEvent(false);

        private volatile Thread _processorThread;
        private volatile CancellationTokenSource _cancellationTokenSource;

        protected readonly ILogger Logger;
       
        protected abstract void ProcessMessageBatch(List<TMessage> messages, CancellationToken cancellationToken);

        public event Action<Exception> Error;

        protected BatchProcessor(int batchSize, ILogger logger)
        {
            _batchSize = batchSize;
            _messages = new ConcurrentQueue<TMessage>();

            Logger = logger ?? new DefaultLogger<BatchProcessor<TMessage>>();
        }

        public virtual void Dispose()
        {
            Stop();

            _messageAddedSignal.Dispose();
            _stoppingSignal.Dispose();
        }

        public void Start()
        {
            lock (_startStopLock)
            {
                if (_processorThread != null) return;

                Logger.LogDebug($"Starting batch processor");

                _cancellationTokenSource = new CancellationTokenSource();

                _processorThread = new Thread(ProcessorFunction) { IsBackground = true };
                _processorThread.Start();

                OnStarted();

                Logger.LogDebug($"Batch processor started");
            }
        }

        public void Stop()
        {
            lock (_startStopLock)
            {
                if (_processorThread == null) return;

                Logger.LogDebug($"Stopping batch processor");

                _cancellationTokenSource.Cancel();
                _messageAddedSignal.Set(); //to speed up the shutdown
            
                _stoppingSignal.WaitOne();

                Logger.LogDebug($"Batch processor stopped");
            }
        }

        /// <summary>
        /// Adds a message to the processing queue.
        /// </summary>
        /// <param name="message">The message to be added to the processing queue</param>
        protected void AddMessage(TMessage message)
        {
            _messages.Enqueue(message);
            _messageAddedSignal.Set();
        }

        protected void ClearQueue()
        {
            while (_messages.TryDequeue(out _)) ;
        }

        protected virtual void OnStarted()
        {
        }

        protected virtual void OnStopped()
        {
        }

        /// <summary>
        /// Call this if an action needs to be done without being interrupted by stop/start.
        /// Use with caution to avoid deadlocks!!!
        /// </summary>
        /// <param name="action"></param>
        protected void DoInStartStopLock(Action action)
        {
            lock (_startStopLock)
            {
                action();
            }
        }

        private void ProcessorFunction()
        {
            Exception exceptionRaised = null;

            try
            {
                while (!_cancellationTokenSource.IsCancellationRequested)
                {
                    _messageAddedSignal.WaitOne(1000);

                    while (!_cancellationTokenSource.IsCancellationRequested && _messages.Count > 0)
                    {
                        var batch = new List<TMessage>();

                        while (!_cancellationTokenSource.IsCancellationRequested && batch.Count < _batchSize && _messages.TryDequeue(out var message))
                        {
                            batch.Add(message);
                        }

                        if (batch.Count > 0)
                        {
                            ProcessMessageBatch(batch, _cancellationTokenSource.Token);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Unhandled exception in batch processor function");
                exceptionRaised = ex;
            }

            _cancellationTokenSource.SafeDispose();
            _cancellationTokenSource = null;
            _processorThread = null;

            OnStopped();

            ClearQueue();

            _stoppingSignal.Set();

            if (exceptionRaised != null)
            {
                OnError(exceptionRaised);
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
    }
}
