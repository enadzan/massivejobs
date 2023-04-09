using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging;

namespace MassiveJobs.Core
{
    public abstract class BatchProcessor<TMessage>: IDisposable
    {
        protected int BatchSize = 100;

        private readonly ManualResetEvent _stoppingSignal = new ManualResetEvent(true);
        private readonly object _startStopLock = new object();

        private readonly ConcurrentQueue<TMessage> _messages;
        private readonly AutoResetEvent _messageAddedSignal = new AutoResetEvent(false);

        private volatile Thread _processorThread;
        private volatile CancellationTokenSource _cancellationTokenSource;

        protected readonly ILogger Logger;
       
        protected abstract void ProcessMessageBatch(List<TMessage> messages, CancellationToken cancellationToken, out int pauseSec);

        public event Action<Exception> Error;

        protected BatchProcessor(ILogger logger)
        {
            Logger = logger;
            _messages = new ConcurrentQueue<TMessage>();
        }

        public virtual void Dispose()
        {
            BeginStop();
            WaitUntilStopped();

            _messageAddedSignal.Dispose();
            _stoppingSignal.Dispose();
        }

        public void Start()
        {
            lock (_startStopLock)
            {
                if (_processorThread != null) return;

                OnStart();

                _stoppingSignal.Reset();

                Logger.LogDebug("Starting batch processor");

                _cancellationTokenSource = new CancellationTokenSource();

                _processorThread = new Thread(ProcessorFunction) {IsBackground = true};
                _processorThread.Start();

                Logger.LogDebug("Batch processor started");
            }
        }

        public void BeginStop()
        {
            lock (_startStopLock)
            {
                if (_processorThread == null) return;

                Logger.LogDebug($"Stopping batch processor");

                OnStopBegin();

                _cancellationTokenSource.Cancel();

                _messageAddedSignal.Set(); //to speed up the shutdown
            }
        }

        public void WaitUntilStopped()
        {
            lock (_startStopLock)
            {
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
            while (_messages.TryDequeue(out _))
            {
            }
        }

        protected virtual void OnStart()
        {
        }

        protected virtual void OnStopBegin()
        {
        }

        protected virtual void OnStop()
        {
        }

        protected virtual void OnPause()
        {
        }

        protected virtual void OnResume()
        {
        }

        private void ProcessorFunction()
        {
            Exception exceptionRaised = null;

            try
            {
                do
                {
                    _messageAddedSignal.WaitOne(1000);

                    while (_messages.Count > 0 && !_cancellationTokenSource.IsCancellationRequested)
                    {
                        var batch = new List<TMessage>();

                        while (batch.Count < BatchSize && _messages.TryDequeue(out var message))
                        {
                            batch.Add(message);
                        }

                        if (batch.Count > 0)
                        {
                            ProcessMessageBatch(batch, _cancellationTokenSource.Token, out var pauseSec);

                            if (pauseSec > 0)
                            {
                                OnPause();
                                Task.Delay(pauseSec * 1000, _cancellationTokenSource.Token).Wait();
                                OnResume();
                            }
                        }
                    }
                }
                while (!_cancellationTokenSource.IsCancellationRequested);
            }
            catch (Exception ex)
            {
                Logger.LogCritical(ex, "Unhandled exception in batch processor function");
                exceptionRaised = ex;
            }

            _cancellationTokenSource.SafeDispose();
            _cancellationTokenSource = null;

            _processorThread = null;

            TryOnStop();

            _stoppingSignal.Set();

            if (exceptionRaised != null)
            {
                OnError(exceptionRaised);
            }
        }

        private void TryOnStop()
        {
            try
            {
                OnStop();
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Exception raised while executing OnStopped");
            }
        }

        protected void OnError(Exception ex)
        {
            try
            {
                #if DEBUG
                Console.WriteLine(ex);
                #endif

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
