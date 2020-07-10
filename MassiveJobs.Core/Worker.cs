using System.Collections.Generic;
using System.Threading;

namespace MassiveJobs.Core
{
    public abstract class Worker : BatchProcessor<RawMessage>, IWorker
    {
        private readonly IMessageConsumer _messageConsumer;

        private volatile IMessageReceiver _messageReceiver;

        protected readonly string QueueName;
        protected readonly IJobServiceScopeFactory ServiceScopeFactory;
       
        protected abstract void ProcessMessageBatch(List<RawMessage> messages, IJobServiceScope serviceScope, CancellationToken cancellationToken, out int pauseSec);

        protected Worker(string queueName, int batchSize, IMessageConsumer messageConsumer, IJobServiceScopeFactory serviceScopeFactory, IJobLogger logger)
            : base(batchSize, logger)
        {
            _messageConsumer = messageConsumer;

            QueueName = queueName;
            ServiceScopeFactory = serviceScopeFactory;
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

        protected bool TryDeserializeJob(RawMessage rawMessage, IJobServiceScope serviceScope, out JobInfo job)
        {
            job = null;

            var argsTag = rawMessage.TypeTag;
            if (argsTag == null || argsTag == string.Empty) return false;

            var serializer = serviceScope.GetRequiredService<IJobSerializer>();
            var typeProvider = serviceScope.GetRequiredService<IJobTypeProvider>();

            job = serializer.Deserialize(rawMessage.Body, argsTag, typeProvider);

            return job != null;
        }

        protected void RunJobs(IReadOnlyList<JobInfo> batch, IJobServiceScope serviceScope, CancellationToken cancellationToken)
        {
            if (batch.Count > 0)
            {
                var jobRunner = serviceScope.GetRequiredService<IJobRunner>();
                var jobPublisher = serviceScope.GetRequiredService<IJobPublisher>();

                jobRunner.RunJobs(jobPublisher, batch, serviceScope, cancellationToken);
            }
        }

        protected override void ProcessMessageBatch(List<RawMessage> messages, CancellationToken cancellationToken, out int pauseSec)
        {
            var serviceScope = ServiceScopeFactory.CreateScope();
            try
            {
                ProcessMessageBatch(messages, serviceScope, cancellationToken, out pauseSec);
            }
            finally
            {
                serviceScope.SafeDispose();
            }
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
