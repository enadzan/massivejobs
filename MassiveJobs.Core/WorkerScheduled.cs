using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;

namespace MassiveJobs.Core
{
    public class WorkerScheduled : Worker
    {
        private const int CheckIntervalMs = 100;

        private readonly AutoResetEvent _stoppingSignal = new AutoResetEvent(false);

        private readonly ConcurrentDictionary<ulong, JobInfo> _scheduledJobs;
        private readonly ConcurrentDictionary<string, List<ulong>> _periodicJobIds;

        private Timer _timer;

        private CancellationTokenSource _cancellationTokenSource;

        public WorkerScheduled(
            IMessageBroker messageBroker,
            string queueName,
            int batchSize,
            IJobPublisher jobPublisher,
            IJobRunner jobRunner,
            IJobSerializer serializer,
            IJobTypeProvider typeProvider,
            IServiceScopeFactory scopeFactory,
            ILogger logger)
            : base(messageBroker, queueName, batchSize, jobPublisher, jobRunner, serializer, typeProvider, scopeFactory, logger)
        {
            _timer = new Timer(CheckScheduledJobs);
            _scheduledJobs = new ConcurrentDictionary<ulong, JobInfo>();
            _periodicJobIds = new ConcurrentDictionary<string, List<ulong>>();
        }

        protected override void OnStart()
        {
            base.OnStart();

            _cancellationTokenSource = new CancellationTokenSource();
            _scheduledJobs.Clear();
            _timer.Change(CheckIntervalMs, Timeout.Infinite);
        }

        protected override void OnStop()
        {
            _cancellationTokenSource.Cancel();
            _stoppingSignal.WaitOne();

            base.OnStop();
        }

        protected override void ProcessMessageBatch(List<RawMessage> messages, IServiceScope serviceScope, CancellationToken cancellationToken, out int pauseSec)
        {
            pauseSec = 0;

            foreach (var rawMessage in messages)
            {
                if (!TryDeserializeJob(rawMessage, out var job))
                {
                    throw new Exception($"Unknown job type: {rawMessage.TypeTag}.");
                }

                if (job.PeriodicRunInfo != null)
                {
                    // periodic jobs should not be added twice
                    if (_periodicJobIds.TryGetValue(job.PeriodicRunInfo.RunId, out var duplicateTags))
                    {
                        duplicateTags.Add(rawMessage.DeliveryTag);
                        continue;
                    }

                    duplicateTags = new List<ulong>();

                    _periodicJobIds.TryAdd(job.PeriodicRunInfo.RunId, duplicateTags);

                    if (!job.PeriodicRunInfo.SetNextRunTime(job.RunAtUtc, DateTime.UtcNow))
                    {
                        duplicateTags.Add(rawMessage.DeliveryTag);
                        continue;
                    }
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
                            _periodicJobIds.TryRemove(job.PeriodicRunInfo.RunId, out _);
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
            foreach (var kvp in _periodicJobIds)
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
                using (var serviceScope = ServiceScopeFactory.SafeCreateScope())
                {
                    JobRunner.RunJobs(JobPublisher, batch.Values, serviceScope, _cancellationTokenSource.Token)
                        .Wait();

                    foreach (var kvp in batch)
                    {
                        // don't confirm periodic jobs, unless passed end time
                        if (kvp.Value.PeriodicRunInfo != null && kvp.Value.PeriodicRunInfo.NextRunTime != DateTime.MinValue) continue;

                        OnMessageProcessed(kvp.Key);
                    }
                }
            }
        }
    }
}
