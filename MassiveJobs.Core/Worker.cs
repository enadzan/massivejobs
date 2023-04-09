using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace MassiveJobs.Core
{
    public abstract class Worker : BatchProcessor<RawMessage>, IWorker
    {
        private readonly IMessageConsumer _messageConsumer;
        private readonly WorkerType _workerType;
        private readonly int _maxDegreeOfParalelism;

        protected volatile IMessageReceiver MessageReceiver;

        protected readonly string QueueName;
        protected readonly IServiceProvider ServiceProvider;
        protected readonly MassiveJobsSettings Settings;

        protected Worker(WorkerType workerType, int index, IServiceProvider serviceProvider, ILogger logger)
            : base(logger)
        {
            _workerType = workerType;
            ServiceProvider = serviceProvider;

            _messageConsumer = serviceProvider.GetRequiredService<IMessageConsumer>();
            Settings = serviceProvider.GetRequiredService<MassiveJobsSettings>();

            QueueName = GetQueueName(index);
            BatchSize = GetBatchSize();

            if (_workerType == WorkerType.Immedate || _workerType == WorkerType.LongRunning)
            {
                _maxDegreeOfParalelism = Settings.MaxDegreeOfParallelismPerWorker;
            }
            else
            {
                _maxDegreeOfParalelism = 1;
            }
        }

        public override void Dispose()
        {
            base.Dispose();
        }

        protected virtual string GetQueueName(int index)
        {
            string template;
            switch (_workerType)
            {
                case WorkerType.Immedate:
                    template = Settings.ImmediateQueueNameTemplate;
                    break;
                case WorkerType.LongRunning:
                    template = Settings.LongRunningQueueNameTemplate;
                    break;
                case WorkerType.Scheduled:
                    template = Settings.ScheduledQueueNameTemplate;
                    break;
                case WorkerType.Periodic:
                    template = Settings.PeriodicQueueNameTemplate;
                    break;
                case WorkerType.Error:
                    return Settings.ErrorQueueName;
                default:
                    throw new ArgumentOutOfRangeException("workerType");
            }

            return string.Format(template, index);
        }

        protected virtual int GetBatchSize()
        {
            switch (_workerType)
            {
                case WorkerType.Immedate:
                    return Settings.ImmediateWorkersBatchSize;
                case WorkerType.LongRunning:
                    return Settings.LongRunningWorkersBatchSize;
                case WorkerType.Scheduled:
                case WorkerType.Error:
                    return Settings.ScheduledWorkersBatchSize;
                case WorkerType.Periodic:
                    return Settings.PeriodicWorkersBatchSize;
                default:
                    throw new ArgumentOutOfRangeException("workerType");
            }
        }

        protected override void OnStart()
        {
            base.OnStart();
            CreateReceiver();
        }

        protected override void OnResume()
        {
            base.OnResume();
            CreateReceiver();
        }

        protected override void OnStop()
        {
            DisposeReceiver();
            base.OnStop();
        }

        protected override void OnPause()
        {
            DisposeReceiver();
            base.OnPause();
        }

        protected void CreateReceiver()
        {
            if (MessageReceiver != null) return;

            MessageReceiver = _messageConsumer.CreateReceiver(QueueName);
            MessageReceiver.MessageReceived += OnMessageReceived;
            MessageReceiver.Start();
        }

        protected void DisposeReceiver()
        {
            if (MessageReceiver == null) return;

            MessageReceiver.MessageReceived -= OnMessageReceived;
            MessageReceiver.SafeDispose(Logger);
            MessageReceiver = null;

            ClearQueue();
        }

        protected bool TryDeserializeJob(RawMessage rawMessage, IServiceScope scope, out JobInfo job)
        {
            job = null;

            var argsTag = rawMessage.TypeTag;
            if (string.IsNullOrEmpty(argsTag)) return false;

            var serializer = scope.ServiceProvider.GetRequiredService<IJobSerializer>();
            var typeProvider = scope.ServiceProvider.GetRequiredService<IJobTypeProvider>();

            job = serializer.Deserialize(rawMessage.Body, argsTag, typeProvider);

            return job != null;
        }

        protected void RunJobs(IReadOnlyList<RawMessage> batch, CancellationToken cancellationToken)
        {
            var parallelOptions = new ParallelOptions
            {
                CancellationToken = cancellationToken,
                MaxDegreeOfParallelism = _maxDegreeOfParalelism
            };

            try 
            {
                Parallel.ForEach(batch, parallelOptions, msg =>
                {
                    using (var scope = ServiceProvider.CreateScope())
                    {
                        if (!TryDeserializeJob(msg, scope, out var job))
                        {
                            throw new Exception($"Unknown job type: {msg.TypeTag}.");
                        }

                        var jobRunner = scope.ServiceProvider.GetRequiredService<IJobRunner>();
                        var jobPublisher = scope.ServiceProvider.GetRequiredService<IJobPublisher>();

                        jobRunner.RunJob(jobPublisher, MessageReceiver, job, msg.DeliveryTag, scope, cancellationToken);
                    }
                });
            }
            catch (OperationCanceledException ex)
            {
                // ignore errors caused by stopping this worker
                if (!cancellationToken.IsCancellationRequested) throw;

                Logger.LogDebug(ex, "Cancelled parallel jobs run");
            }
        }

        protected void OnBatchProcessed(ulong lastDeliveryTag)
        {
            MessageReceiver.AckBatchProcessed(lastDeliveryTag);
        }

        protected void OnMessageProcessed(IServiceScope scope, ulong deliveryTag)
        {
            MessageReceiver.AckMessageProcessed(scope, deliveryTag);
        }

        private void OnMessageReceived(IMessageReceiver sender, RawMessage message)
        {
            // constructing string is expensive and this is hot path
            if (Logger.IsEnabled(LogLevel.Trace))
            {
                Logger.LogTrace($"Message received on worker {QueueName}");
            }

            AddMessage(message);
        }
    }
}
