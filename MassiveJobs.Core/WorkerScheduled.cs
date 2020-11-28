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

        private readonly Timer _timer;

        private CancellationTokenSource _cancellationTokenSource;

        public WorkerScheduled(
            string queueName, 
            int batchSize, 
            IMessageConsumer messageConsumer, 
            IJobServiceScopeFactory serviceScopeFactory, 
            IJobLogger<WorkerScheduled> logger)
            : base(queueName, batchSize, 1, messageConsumer, serviceScopeFactory, logger)
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

        protected override void ProcessMessageBatch(List<RawMessage> messages, CancellationToken cancellationToken, out int pauseSec)
        {
            pauseSec = 0;

            using (var scope = ServiceScopeFactory.CreateScope())
            {
                foreach (var rawMessage in messages)
                {
                    if (!TryDeserializeJob(rawMessage, scope, out var job))
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

                        // Periodic jobs should not be added twice. 
                        // If the new job has "LastRunTime", it will replace the old job, otherwise it is discarded.
                        // This is because we don't want restarts of applications (that will schedule a periodic job without "LastRunTime")
                        // to interfere with the execution of jobs.

                        if (_periodicJobs.TryGetValue(job.GroupKey, out var runningTag))
                        {
                            if (!job.PeriodicRunInfo.LastRunTimeUtc.HasValue)
                            {
                                duplicateTags.Add(rawMessage.DeliveryTag);
                                continue;
                            }

                            _periodicJobs.TryRemove(job.GroupKey, out _);
                            _scheduledJobs.TryRemove(runningTag, out _);

                            duplicateTags.Add(runningTag);
                        }

                        try
                        {
                            if (!job.PeriodicRunInfo.SetNextRunTime(job.RunAtUtc, DateTime.UtcNow))
                            {
                                duplicateTags.Add(rawMessage.DeliveryTag);
                                continue;
                            }
                        }
                        catch (Exception ex)
                        {
                            // Setting the next time could throw in cron expressions.
                            // It shouldn't happen, but if it does, log error and skip.
                            Logger.LogError(ex,
                                $"Failed setting next run time in job with group key '{job.GroupKey}' (last run utc: {job.PeriodicRunInfo.LastRunTimeUtc})");
                            duplicateTags.Add(rawMessage.DeliveryTag);
                            continue;
                        }

                        _periodicJobs.TryAdd(job.GroupKey, rawMessage.DeliveryTag);
                    }

                    _scheduledJobs.TryAdd(rawMessage.DeliveryTag, job);
                }
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

                PublishAsImmediateJobs(batchToRun);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, $"Error in scheduled worker: {QueueName}");
                
                // we must call this before OnError to avoid deadlock
                // when the error occurred while the worker is stopping

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

        private void PublishAsImmediateJobs(Dictionary<ulong, JobInfo> batch)
        {
            if (batch.Count == 0) return;

            using (var scope = ServiceScopeFactory.CreateScope())
            {
                var jobPublisher = scope.GetRequiredService<IJobPublisher>();
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
