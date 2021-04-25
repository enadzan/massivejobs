using System.Collections.Generic;
using System.Threading;
using MassiveJobs.Core.DependencyInjection;

namespace MassiveJobs.Core
{
    public sealed class WorkerImmediate : Worker
    {
        public WorkerImmediate(
            string queueName, 
            int batchSize, 
            int maxDegreeOfParallelism,
            IMessageConsumer messageConsumer, 
            IJobServiceScopeFactory serviceScopeFactory, 
            IJobLogger<WorkerImmediate> logger)
            : base(queueName, batchSize, maxDegreeOfParallelism, false, messageConsumer, serviceScopeFactory, logger)
        {
        }

        protected override void ProcessMessageBatch(List<RawMessage> messages, CancellationToken cancellationToken, out int pauseSec)
        {
            pauseSec = 0;
            if (messages.Count == 0) return;

            RunJobs(messages, cancellationToken);

            // If we we are interrupted, we don't know if the batch is completely processed,
            // so we can't send confirmations.
            if (cancellationToken.IsCancellationRequested) return;

            OnBatchProcessed(messages[messages.Count - 1].DeliveryTag);
        }
    }
}
