using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MassiveJobs.Core.DependencyInjection;

namespace MassiveJobs.Core
{
    public abstract class Worker : BatchProcessor<RawMessage>, IWorker
    {
        private readonly IMessageConsumer _messageConsumer;
        private readonly int _maxDegreeOfParallelism;
        private readonly bool _singleActiveConsumer;
        protected volatile IMessageReceiver MessageReceiver;

        protected readonly string QueueName;
        protected readonly IJobServiceScopeFactory ServiceScopeFactory;
        protected readonly IJobServiceScope ServiceScope;

        protected Worker(string queueName, int batchSize, int masMaxDegreeOfParallelism, bool singleActiveConsumer,
            IJobServiceScopeFactory serviceScopeFactory, IJobLogger<Worker> logger)
            : base(batchSize, logger)
        {
            ServiceScopeFactory = serviceScopeFactory;
            ServiceScope = ServiceScopeFactory.CreateScope();

            _maxDegreeOfParallelism = masMaxDegreeOfParallelism;
            _singleActiveConsumer = singleActiveConsumer;
            _messageConsumer = ServiceScope.GetRequiredService<IMessageConsumer>();

            QueueName = queueName;
        }

        public override void Dispose()
        {
            base.Dispose();
            ServiceScope.Dispose();
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

            MessageReceiver = _messageConsumer.CreateReceiver(QueueName, _singleActiveConsumer);
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

        protected bool TryDeserializeJob(RawMessage rawMessage, IJobServiceScope scope, out JobInfo job)
        {
            job = null;

            var argsTag = rawMessage.TypeTag;
            if (string.IsNullOrEmpty(argsTag)) return false;

            var serializer = scope.GetRequiredService<IJobSerializer>();
            var typeProvider = scope.GetRequiredService<IJobTypeProvider>();

            job = serializer.Deserialize(rawMessage.Body, argsTag, typeProvider);

            return job != null;
        }

        protected void RunJobs(IReadOnlyList<RawMessage> batch, CancellationToken cancellationToken)
        {
            var parallelOptions = new ParallelOptions
            {
                CancellationToken = cancellationToken,
                MaxDegreeOfParallelism = _maxDegreeOfParallelism
            };

            try 
            {
                Parallel.ForEach(batch, parallelOptions, msg =>
                {
                    using (var scope = ServiceScopeFactory.CreateScope())
                    {
                        if (!TryDeserializeJob(msg, scope, out var job))
                        {
                            throw new Exception($"Unknown job type: {msg.TypeTag}.");
                        }

                        var jobRunner = scope.GetRequiredService<IJobRunner>();
                        var jobPublisher = scope.GetRequiredService<IJobPublisher>();

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

        protected void OnMessageProcessed(IJobServiceScope scope, ulong deliveryTag)
        {
            MessageReceiver.AckMessageProcessed(scope, deliveryTag);
        }

        private void OnMessageReceived(IMessageReceiver sender, RawMessage message)
        {
            // constructing string is expensive and this is hot path
            if (Logger.IsEnabled(JobLogLevel.Trace))
            {
                Logger.LogTrace($"Message received on worker {QueueName}");
            }

            AddMessage(message);
        }
    }
}
