﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace MassiveJobs.Core
{
    public sealed class WorkerScheduled : Worker
    {
        private const int CheckIntervalMs = 250;

        private readonly ManualResetEvent _stoppingSignal = new ManualResetEvent(true);

        private readonly ConcurrentDictionary<ulong, JobInfo> _scheduledJobs;
        private readonly ConcurrentDictionary<string, ConcurrentBag<ulong>> _periodicSkipJobs;
        private readonly ConcurrentDictionary<string, ulong> _periodicJobs;

        private Thread _thread;

        private volatile int _started;

        public WorkerScheduled(WorkerType workerType, int index, IServiceProvider serviceProvider)
            : base(workerType, index, serviceProvider, serviceProvider.GetRequiredService<ILogger<WorkerScheduled>>())
        {
            _scheduledJobs = new ConcurrentDictionary<ulong, JobInfo>();
            _periodicJobs = new ConcurrentDictionary<string, ulong>();
            _periodicSkipJobs = new ConcurrentDictionary<string, ConcurrentBag<ulong>>();
        }

        public override void Dispose()
        {
            _stoppingSignal.SafeDispose(Logger);

            base.Dispose();
        }

        protected override void OnStart()
        {
            base.OnStart();

            var previousValue = Interlocked.Exchange(ref _started, 1);
            if (previousValue != 0) return;

            _stoppingSignal.Reset();
            _scheduledJobs.Clear();

            _thread = new Thread(CheckScheduledJobs) { IsBackground = true };
            _thread.Start();
        }

        protected override void OnStopBegin()
        {
            var previousValue = Interlocked.Exchange(ref _started, 0);
            if (previousValue == 0) return;

            _stoppingSignal.WaitOne();

            base.OnStopBegin();
        }

        protected override void ProcessMessageBatch(List<RawMessage> messages, CancellationToken cancellationToken, out int pauseSec)
        {
            pauseSec = 0;

            using (var scope = ServiceProvider.CreateScope())
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

        private void CheckScheduledJobs()
        {
            Exception raisedException = null;

            while (_started != 0)
            {
                Thread.Sleep(CheckIntervalMs);

                try
                {
                    ConfirmSkippedMessages();

                    var batchToRun = new Dictionary<ulong, JobInfo>();

                    var now = DateTime.UtcNow;

                    foreach (var kvp in _scheduledJobs)
                    {
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
                    break;
                }
            }
            
            _stoppingSignal.Set();

            if (raisedException != null)
            {
                OnError(raisedException);
            }
        }

        private void ConfirmSkippedMessages()
        {
            using (var scope = ServiceProvider.CreateScope())
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

            using (var scope = ServiceProvider.CreateScope())
            {
                var jobPublisher = scope.ServiceProvider.GetRequiredService<IJobPublisher>();
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
                    OnMessageProcessed(scope, kvp.Key);
                }
            }
        }
    }
}
