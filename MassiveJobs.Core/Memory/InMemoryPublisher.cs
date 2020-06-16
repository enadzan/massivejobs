using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;

namespace MassiveJobs.Core.Memory
{
    public class InMemoryPublisher : IJobPublisher, IDisposable
    {
        private bool _running = true;

        private readonly DefaultJobRunner _jobRunner;

        private readonly ConcurrentQueue<JobInfo> _envelopesQueue = new ConcurrentQueue<JobInfo>();

        private readonly AutoResetEvent _waitJobEvent = new AutoResetEvent(false);
        private readonly ManualResetEvent _waitCompletedEvent = new ManualResetEvent(false);
        private readonly IServiceScopeFactory _serviceScopeFactory;
        private Thread _dispatcherThread;

        private CancellationTokenSource _cancellationTokenSource;

        public int JobsCount => _envelopesQueue.Count;
        public JobInfo JobsTop 
        { 
            get
            {
                if (!_envelopesQueue.TryDequeue(out var job)) return null;
                return job;
            } 
        }

        public InMemoryPublisher(IServiceScopeFactory serviceScopeFactory = null)
        {
            _serviceScopeFactory = serviceScopeFactory;
            _jobRunner = new DefaultJobRunner();
        }

        public void StartJobWorkers()
        {
            if (_cancellationTokenSource != null) return;

            _cancellationTokenSource = new CancellationTokenSource();

            _dispatcherThread = new Thread(DispatcherThreadMethod) { IsBackground = true };
            _dispatcherThread.Start();
        }

        public void StopJobWorkers()
        {
            if (_cancellationTokenSource == null) return;

            _waitCompletedEvent.Reset();

            _running = false;
            _cancellationTokenSource.Cancel();
            _waitJobEvent.Set();

            WaitCompleted();

            _cancellationTokenSource.Dispose();
            _cancellationTokenSource = null;
        }

        public void Publish(IEnumerable<JobInfo> jobs)
        {
            foreach (var jobInfo in jobs)
            {
                _envelopesQueue.Enqueue(jobInfo);
            }

             _waitCompletedEvent.Reset();
            _waitJobEvent.Set();
        }

        public void Dispose()
        {
            StopJobWorkers();
        }

        /// <summary>
        /// Wait until all jobs that should be processed immediately are processed.
        /// This method will not wait for the jobs scheduled in the future.
        /// </summary>
        /// <param name="timeout">Timeout in milliseconds. Use <see cref="Timeout.Infinite"/> to wait indefinitely.</param>
        /// <returns>true if all jobs were processed before timeout, false otherwise</returns>
        public bool WaitCompleted(int timeoutMs = Timeout.Infinite)
        {
            return _waitCompletedEvent.WaitOne(timeoutMs);
        }

        private void DispatcherThreadMethod()
        {
            while (_running && !_cancellationTokenSource.IsCancellationRequested)
            {
                _waitJobEvent.WaitOne();

                var performedCount = OnJobsReceived();

                if (_envelopesQueue.Count == 0 || performedCount == 0)
                {
                    _waitCompletedEvent.Set();
                }
            }

            _waitCompletedEvent.Set();
        }

        private int OnJobsReceived()
        {
            var batch = new List<JobInfo>();

            var requeueList = new List<JobInfo>();

            var now = DateTime.UtcNow;

            while (batch.Count < 100_000 && _envelopesQueue.TryDequeue(out var jobInfo))
            {
                if (jobInfo.RunAtUtc.HasValue && jobInfo.RunAtUtc.Value > now)
                {
                    requeueList.Add(jobInfo);
                }
                else
                {
                    batch.Add(jobInfo);
                }
            }

            using (var serviceScope = _serviceScopeFactory.SafeCreateScope())
            {
                var runTask = _jobRunner.RunJobs(this, batch, serviceScope, _cancellationTokenSource.Token);

                foreach (var jobInfo in requeueList)
                {
                    _envelopesQueue.Enqueue(jobInfo);
                }

                runTask.Wait();
            }

            return batch.Count;
        }
    }
}
