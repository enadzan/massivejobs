using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace MassiveJobs.Core
{
    public sealed class WorkerScheduled : Worker
    {
        private const int CheckIntervalMs = 100;

        private readonly ManualResetEvent _stoppingSignal = new ManualResetEvent(false);

        private readonly ConcurrentDictionary<ulong, JobInfo> _scheduledJobs;
        private readonly ConcurrentDictionary<string, List<ulong>> _periodicSkipJobs;
        private readonly ConcurrentDictionary<string, ulong> _periodicJobs;

        private Timer _timer;

        private CancellationTokenSource _cancellationTokenSource;

        public WorkerScheduled(
            string queueName, 
            int batchSize, 
            IMessageConsumer messageConsumer, 
            IServiceScopeFactory serviceScopeFactory, 
            ILogger logger)
            : base(queueName, batchSize, messageConsumer, serviceScopeFactory, logger)
        {
            _timer = new Timer(CheckScheduledJobs);
            _scheduledJobs = new ConcurrentDictionary<ulong, JobInfo>();
            _periodicJobs = new ConcurrentDictionary<string, ulong>();
            _periodicSkipJobs = new ConcurrentDictionary<string, List<ulong>>();
        }

        protected override void OnStart()
        {
            base.OnStart();

            _stoppingSignal.Reset();

            _cancellationTokenSource = new CancellationTokenSource();
            _scheduledJobs.Clear();
            _timer.Change(CheckIntervalMs, Timeout.Infinite);
        }

        protected override void OnStopBegin()
        {
            _cancellationTokenSource.Cancel();
            _stoppingSignal.WaitOne();

            base.OnStopBegin();
        }

        protected override void ProcessMessageBatch(List<RawMessage> messages, IServiceScope serviceScope, CancellationToken cancellationToken, out int pauseSec)
        {
            pauseSec = 0;

            foreach (var rawMessage in messages)
            {
                if (!TryDeserializeJob(rawMessage, serviceScope, out var job))
                {
                    throw new Exception($"Unknown job type: {rawMessage.TypeTag}.");
                }

                if (job.PeriodicRunInfo != null)
                {
                    if (!_periodicSkipJobs.TryGetValue(job.GroupKey, out var duplicateTags))
                    {
                        duplicateTags = new List<ulong>();
                    }

                    // Periodic jobs should not be added twice. New job will be added, old job will be confirmed.
                    // This will enable cancelling of the periodic jobs, by sending a new job with the same GroupKey.

                    if (_periodicJobs.TryGetValue(job.GroupKey, out var runningTag))
                    {
                        _periodicJobs.TryRemove(job.GroupKey, out _);
                        _scheduledJobs.TryRemove(runningTag, out _);

                        duplicateTags.Add(runningTag);
                    }

                    if (!job.PeriodicRunInfo.SetNextRunTime(job.RunAtUtc, DateTime.UtcNow))
                    {
                        duplicateTags.Add(rawMessage.DeliveryTag);
                        continue;
                    }

                    _periodicJobs.TryAdd(job.GroupKey, rawMessage.DeliveryTag);
                }

                _scheduledJobs.TryAdd(rawMessage.DeliveryTag, job);
            }
        }

        private void CheckScheduledJobs(object state)
        {
            try
            {
                ConfirmSkippedMessages();

                var batchToRun = new Dictionary<ulong, JobInfo>();

                var now = DateTime.UtcNow;

                foreach (var key in _scheduledJobs.Keys)
                {
                    if (_cancellationTokenSource.IsCancellationRequested) return;

                    if (!_scheduledJobs.TryGetValue(key, out var job)) continue;
                    if (job.RunAtUtc > now) continue;
                    if (job.PeriodicRunInfo != null && job.PeriodicRunInfo.NextRunTime > now) continue;

                    batchToRun.Add(key, job);

                    if (job.PeriodicRunInfo != null)
                    {
                        if (!job.PeriodicRunInfo.SetNextRunTime(job.RunAtUtc, now))
                        {
                            _scheduledJobs.TryRemove(key, out _);
                            _periodicSkipJobs.TryRemove(job.GroupKey, out _);
                        }
                    }
                    else
                    {
                        _scheduledJobs.TryRemove(key, out _);
                    }
                }

                RunBatch(batchToRun);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, $"Error in scheduled worker: {QueueName}");
                
                // we must call this before OnError to avoid deadlock
                // when the error occured while the worker is stopping

                _stoppingSignal.Set();

                OnError(ex);
            }
            finally
            {
                if (!_cancellationTokenSource.IsCancellationRequested)
                {
                    _timer.Change(CheckIntervalMs, Timeout.Infinite);
                }
                else
                {
                    _cancellationTokenSource.SafeDispose();
                    _cancellationTokenSource = null;

                    _stoppingSignal.Set();
                }
            }
        }

        private void ConfirmSkippedMessages()
        {
            foreach (var kvp in _periodicSkipJobs)
            {
                foreach (var deliveryTag in kvp.Value)
                {
                    OnMessageProcessed(deliveryTag);
                }

                kvp.Value.Clear();
            }
        }

        private void RunBatch(Dictionary<ulong, JobInfo> batch)
        {
            if (batch.Count > 0)
            {
                using (var serviceScope = ServiceScopeFactory.CreateScope())
                {
                    RunJobs(batch.Values.ToList(), serviceScope, _cancellationTokenSource.Token);

                    if (_cancellationTokenSource.IsCancellationRequested) return;

                    foreach (var kvp in batch)
                    {
                        // don't confirm periodic jobs, unless passed end time
                        if (kvp.Value.PeriodicRunInfo != null && kvp.Value.PeriodicRunInfo.NextRunTime != DateTime.MinValue) continue;

                        OnMessageProcessed(kvp.Key);

                        _scheduledJobs.TryRemove(kvp.Key, out _);

                        if (kvp.Value.PeriodicRunInfo != null)
                        {
                            _periodicJobs.TryRemove(kvp.Value.GroupKey, out _);
                        }
                    }
                }
            }
        }
    }
}
