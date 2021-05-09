using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using MassiveJobs.Core.DependencyInjection;

namespace MassiveJobs.Core
{
    public sealed class WorkerScheduled : Worker
    {
        private const int CheckIntervalMs = 250;

        private readonly ManualResetEvent _stoppingSignal = new ManualResetEvent(true);

        private readonly ConcurrentDictionary<ulong, JobInfo> _scheduledJobs;
        private readonly ConcurrentDictionary<string, ConcurrentBag<ulong>> _periodicSkipJobs;
        private readonly ConcurrentDictionary<string, ulong> _periodicJobs;

        private readonly ITimer _timer;
        private readonly ITimeProvider _timeProvider;

        private CancellationTokenSource _cancellationTokenSource;

        public WorkerScheduled(string queueName, int batchSize, IJobServiceFactory serviceFactory)
            : base(queueName, batchSize, 1, true, 
                  serviceFactory.GetRequiredService<IMessageConsumer>(),
                  serviceFactory.GetRequiredService<IJobServiceScopeFactory>(),
                  serviceFactory.GetRequiredService<IJobLogger<WorkerScheduled>>())
        {
            _timeProvider = serviceFactory.GetService<ITimeProvider>() ?? new DefaultTimeProvider();

            _timer = serviceFactory.GetService<ITimer>() ?? new DefaultTimer();
            _timer.TimeElapsed += CheckScheduledJobs;

            _scheduledJobs = new ConcurrentDictionary<ulong, JobInfo>();
            _periodicJobs = new ConcurrentDictionary<string, ulong>();
            _periodicSkipJobs = new ConcurrentDictionary<string, ConcurrentBag<ulong>>();
        }

        public override void Dispose()
        {
            _timer.TimeElapsed -= CheckScheduledJobs;
            _timer.SafeDispose(Logger);

            _stoppingSignal.SafeDispose(Logger);

            base.Dispose();
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
                            if (!job.PeriodicRunInfo.SetNextRunTime(job.RunAtUtc, _timeProvider.GetCurrentTimeUtc()))
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

        private void CheckScheduledJobs()
        {
            Exception raisedException = null;

            try
            {
                ConfirmSkippedMessages();

                var batchToRun = new Dictionary<ulong, JobInfo>();

                var now = _timeProvider.GetCurrentTimeUtc();

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
                raisedException = ex;
            }
            finally
            {
                if (raisedException == null && !_cancellationTokenSource.IsCancellationRequested)
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

            if (raisedException != null)
            {
                OnError(raisedException);
            }
        }

        private void ConfirmSkippedMessages()
        {
            using (var scope = ServiceScopeFactory.CreateScope())
            { 
                foreach (var kvp in _periodicSkipJobs)
                {
                    while (kvp.Value.Count > 0)
                    {
                        if (kvp.Value.TryTake(out var deliveryTag))
                        {
                            OnMessageProcessed(scope, deliveryTag);
                        }
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

                periodicJobs.ForEach(j => j.PeriodicRunInfo.LastRunTimeUtc = _timeProvider.GetCurrentTimeUtc());

                jobPublisher.Publish(periodicJobs);

                foreach (var kvp in batch)
                {
                    OnMessageProcessed(scope, kvp.Key);
                }
            }
        }
    }
}
