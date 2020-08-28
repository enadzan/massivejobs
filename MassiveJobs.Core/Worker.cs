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
        protected readonly ScopePool ScopePool;

        protected Worker(string queueName, int batchSize, int masMaxDegreeOfParallelism, 
            IMessageConsumer messageConsumer, IJobServiceScopeFactory serviceScopeFactory, IJobLogger logger)
            : base(batchSize, logger)
        {
            _maxDegreeOfParallelism = masMaxDegreeOfParallelism;
            _messageConsumer = messageConsumer;

            ScopePool = new ScopePool(serviceScopeFactory);

            QueueName = queueName;
        }

        public override void Dispose()
        {
            ScopePool.SafeDispose(Logger);
            base.Dispose();
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

        protected bool TryDeserializeJob(RawMessage rawMessage, out JobInfo job)
        {
            job = null;

            var argsTag = rawMessage.TypeTag;
            if (string.IsNullOrEmpty(argsTag)) return false;

            var poolItem = ScopePool.Get();
            try
            {
                var serializer = poolItem.Scope.GetRequiredService<IJobSerializer>();
                var typeProvider = poolItem.Scope.GetRequiredService<IJobTypeProvider>();

                job = serializer.Deserialize(rawMessage.Body, argsTag, typeProvider);

                ScopePool.Return(ref poolItem);
            }
            catch
            {
                poolItem.SafeDispose(Logger);
                throw;
            }

            return job != null;
        }

        protected void RunJobs(IReadOnlyList<JobInfo> batch, CancellationToken cancellationToken)
        {
            if (batch.Count == 0) return;

            var parallelOptions = new ParallelOptions
            {
                CancellationToken = cancellationToken,
                MaxDegreeOfParallelism = _maxDegreeOfParallelism
            };

            Parallel.ForEach(batch, parallelOptions, job =>
            {
                var poolItem = ScopePool.Get();
                try
                {
                    var jobRunner = poolItem.Scope.GetRequiredService<IJobRunner>();
                    var jobPublisher = poolItem.Scope.GetRequiredService<IJobPublisher>();

                    jobRunner.RunJob(jobPublisher, job, poolItem.Scope, cancellationToken);

                    ScopePool.Return(ref poolItem);
                }
                catch
                {
                    poolItem.SafeDispose(Logger);
                    throw;
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
