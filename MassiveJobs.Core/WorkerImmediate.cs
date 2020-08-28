using System;
using System.Collections.Generic;
using System.Threading;

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
            IJobLogger logger)
            : base(queueName, batchSize, maxDegreeOfParallelism, messageConsumer, serviceScopeFactory, logger)
        {
        }

        protected override void ProcessMessageBatch(List<RawMessage> messages, CancellationToken cancellationToken, out int pauseSec)
        {
            pauseSec = 0;
            ulong? lastDeliveryTag = null;

            var batch = new List<JobInfo>();

            foreach (var rawMessage in messages)
            {
                if (!TryDeserializeJob(rawMessage, out var job))
                {
                    throw new Exception($"Unknown job type: {rawMessage.TypeTag}.");
                }

                batch.Add(job);
                lastDeliveryTag = rawMessage.DeliveryTag;
            }

            RunJobs(batch, cancellationToken);

            if (cancellationToken.IsCancellationRequested) return;

            if (lastDeliveryTag.HasValue)
            {
                OnBatchProcessed(lastDeliveryTag.Value);
            }
        }
    }
}
