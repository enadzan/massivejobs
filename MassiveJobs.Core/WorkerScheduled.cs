using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace MassiveJobs.Core
{
    public class WorkerScheduled : Worker
    {
        private const int CheckIntervalMs = 100;

        private readonly ConcurrentDictionary<ulong, JobInfo> _scheduledJobs;

        private Timer _timer;

        private bool _running;

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
        }

        protected override void OnStarted()
        {
            _scheduledJobs.Clear();
            _running = true;
            _timer.Change(CheckIntervalMs, Timeout.Infinite);
        }

        protected override void OnStopped()
        {
            _running = false;
            _timer.Change(Timeout.Infinite, Timeout.Infinite);
        }

        protected override void ProcessMessageBatch(List<RawMessage> messages, IServiceScope _)
        {
            foreach (var rawMessage in messages)
            {
                if (!TryDeserializeJob(rawMessage, out var job))
                {
                    throw new Exception($"Unknown job type: {rawMessage.TypeTag}.");
                }
                
                _scheduledJobs.TryAdd(rawMessage.DeliveryTag, job);
            }
        }

        private void CheckScheduledJobs(object state)
        {
            try
            {
                var batchToRun = new Dictionary<ulong, JobInfo>();

                var now = DateTime.UtcNow;

                foreach (var key in _scheduledJobs.Keys)
                {
                    if (CancellationToken.IsCancellationRequested) return;

                    if (!_scheduledJobs.TryGetValue(key, out var job)) continue;
                    if (job.RunAtUtc > now) continue;

                    batchToRun.Add(key, job);
                    _scheduledJobs.TryRemove(key, out _);
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
                if (_running)
                {
                    _timer.Change(CheckIntervalMs, Timeout.Infinite);
                }
            }
        }

        private void RunBatch(Dictionary<ulong, JobInfo> batch)
        {
            if (batch.Count > 0)
            {
                using (var serviceScope = ServiceScopeFactory.SafeCreateScope())
                {
                    JobRunner.RunJobs(JobPublisher, batch.Values, serviceScope, CancellationToken)
                        .Wait();

                    foreach (var deliveryTag in batch.Keys)
                    {
                        OnMessageProcessed(deliveryTag);
                    }
                }
            }
        }
    }
}
