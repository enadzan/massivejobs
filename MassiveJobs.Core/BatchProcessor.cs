﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace MassiveJobs.Core
{
    public abstract class BatchProcessor<TMessage>: IDisposable
    {
        private readonly int _batchSize;

        private readonly ManualResetEvent _stoppingSignal = new ManualResetEvent(true);
        private readonly object _startStopLock = new object();

        private readonly ConcurrentQueue<TMessage> _messages;
        private readonly AutoResetEvent _messageAddedSignal = new AutoResetEvent(false);

        private volatile Thread _processorThread;
        private volatile CancellationTokenSource _cancellationTokenSource;
        private volatile CancellationTokenSource _jobsCancellationTokenSource;

        protected readonly IJobLogger Logger;
       
        protected abstract void ProcessMessageBatch(List<TMessage> messages, CancellationToken cancellationToken, out int pauseSec);

        public event Action<Exception> Error;

        protected BatchProcessor(int batchSize, IJobLogger<BatchProcessor<TMessage>> logger)
        {
            _batchSize = batchSize;
            _messages = new ConcurrentQueue<TMessage>();

            Logger = logger;
        }

        public virtual void Dispose()
        {
            BeginStop(true);
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
                _jobsCancellationTokenSource = new CancellationTokenSource();

                _processorThread = new Thread(ProcessorFunction) {IsBackground = true};
                _processorThread.Start();

                Logger.LogDebug("Batch processor started");
            }
        }

        public void BeginStop(bool cancelRunningJobs)
        {
            lock (_startStopLock)
            {
                if (_processorThread == null) return;

                Logger.LogDebug($"Stopping batch processor");

                OnStopBegin(cancelRunningJobs);

                _cancellationTokenSource.Cancel();

                if (cancelRunningJobs) 
                {
                    _jobsCancellationTokenSource.Cancel();
                }

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

        protected virtual void OnStopBegin(bool cancelRunningJobs)
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

                    while (!_jobsCancellationTokenSource.IsCancellationRequested && _messages.Count > 0)
                    {
                        var batch = new List<TMessage>();

                        while (!_jobsCancellationTokenSource.IsCancellationRequested && batch.Count < _batchSize && _messages.TryDequeue(out var message))
                        {
                            batch.Add(message);
                        }

                        if (batch.Count > 0)
                        {
                            ProcessMessageBatch(batch, _jobsCancellationTokenSource.Token, out var pauseSec);

                            if (pauseSec > 0)
                            {
                                OnPause();
                                Task.Delay(pauseSec * 1000, _jobsCancellationTokenSource.Token).Wait();
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

            _jobsCancellationTokenSource.SafeDispose();
            _jobsCancellationTokenSource = null;

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
