using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace MassiveJobs.Core
{
    public abstract class Worker : BatchProcessor<RawMessage>, IWorker
    {
        private readonly IMessageConsumer _messageConsumer;
        private readonly int _maxDegreeOfParallelism;

        private volatile IMessageReceiver _messageReceiver;

        protected readonly string QueueName;
        protected readonly IJobServiceScopeFactory ServiceScopeFactory;

        protected Worker(string queueName, int batchSize, int masMaxDegreeOfParallelism, 
            IMessageConsumer messageConsumer, IJobServiceScopeFactory serviceScopeFactory, IJobLogger<Worker> logger)
            : base(batchSize, logger)
        {
            _maxDegreeOfParallelism = masMaxDegreeOfParallelism;
            _messageConsumer = messageConsumer;
            ServiceScopeFactory = serviceScopeFactory;

            QueueName = queueName;
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
            if (_messageReceiver != null) return;

            _messageReceiver = _messageConsumer.CreateReceiver(QueueName);
            _messageReceiver.MessageReceived += OnMessageReceived;
            _messageReceiver.Start();
        }

        protected void DisposeReceiver()
        {
            if (_messageReceiver == null) return;

            _messageReceiver.MessageReceived -= OnMessageReceived;
            _messageReceiver.SafeDispose(Logger);
            _messageReceiver = null;

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

                    jobRunner.RunJob(jobPublisher, job, scope, cancellationToken);
                }
            });
        }

        protected void OnBatchProcessed(ulong lastDeliveryTag)
        {
            _messageReceiver.AckBatchProcessed(lastDeliveryTag);
        }

        protected void OnMessageProcessed(ulong deliveryTag)
        {
            _messageReceiver.AckMessageProcessed(deliveryTag);
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
