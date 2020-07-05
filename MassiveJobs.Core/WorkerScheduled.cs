using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace MassiveJobs.Core
{
    public sealed class WorkerScheduled : Worker
    {
        private const int CheckIntervalMs = 250;

        private readonly ManualResetEvent _stoppingSignal = new ManualResetEvent(true);

        private readonly ConcurrentDictionary<ulong, JobInfo> _scheduledJobs;
        private readonly ConcurrentDictionary<string, ConcurrentBag<ulong>> _periodicSkipJobs;
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
            _periodicSkipJobs = new ConcurrentDictionary<string, ConcurrentBag<ulong>>();
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
                        duplicateTags = new ConcurrentBag<ulong>();
                        _periodicSkipJobs.TryAdd(job.GroupKey, duplicateTags);
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

                foreach (var kvp in _scheduledJobs)
                {
                    if (_cancellationTokenSource.IsCancellationRequested) return;

                    var deliveryTag = kvp.Key;
                    var job = kvp.Value;

                    if (job.RunAtUtc > now) continue;
                    if (job.PeriodicRunInfo != null && job.PeriodicRunInfo.NextRunTime > now) continue;

                    batchToRun.Add(deliveryTag, job);

                    if (job.PeriodicRunInfo != null) _periodicJobs.TryRemove(job.GroupKey, out _);
                    _scheduledJobs.TryRemove(deliveryTag, out _);
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
                while (kvp.Value.Count > 0)
                {
                    if (kvp.Value.TryTake(out var deliveryTag))
                    {
                        OnMessageProcessed(deliveryTag);
                    }
                }
            }
        }

        private void RunBatch(Dictionary<ulong, JobInfo> batch)
        {
            if (batch.Count > 0)
            {
                using (var serviceScope = ServiceScopeFactory.CreateScope())
                {
                    var jobPublisher = serviceScope.GetService<IJobPublisher>();
                    if (jobPublisher == null) return; // this can happen only this worker is being stopped;

                    jobPublisher.Publish(batch.Select(j => j.Value.ToImmediateJob()));

                    var periodicJobs = batch
                        .Where(j => j.Value.PeriodicRunInfo != null)
                        .Select(j => j.Value)
                        .ToList();

                    periodicJobs.ForEach(j => j.PeriodicRunInfo.LastRunTimeUtc = DateTime.UtcNow);

                    jobPublisher.Publish(periodicJobs);

                    foreach (var kvp in batch)
                    {
                        OnMessageProcessed(kvp.Key);
                    }
                }
            }
        }
    }
}
