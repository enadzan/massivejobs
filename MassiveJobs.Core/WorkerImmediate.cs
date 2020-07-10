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
            IMessageConsumer messageConsumer, 
            IJobServiceScopeFactory serviceScopeFactory, 
            IJobLogger logger)
            : base(queueName, batchSize, messageConsumer, serviceScopeFactory, logger)
        {
        }

        protected override void ProcessMessageBatch(List<RawMessage> messages, IJobServiceScope serviceScope, CancellationToken cancellationToken, out int pauseSec)
        {
            pauseSec = 0;
            ulong? lastDeliveryTag = null;

            var batch = new List<JobInfo>();

            foreach (var rawMessage in messages)
            {
                if (!TryDeserializeJob(rawMessage, serviceScope, out var job))
                {
                    throw new Exception($"Unknown job type: {rawMessage.TypeTag}.");
                }

                batch.Add(job);
                lastDeliveryTag = rawMessage.DeliveryTag;
            }

            RunJobs(batch, serviceScope, cancellationToken);

            if (cancellationToken.IsCancellationRequested) return;

            if (lastDeliveryTag.HasValue)
            {
                OnBatchProcessed(lastDeliveryTag.Value);
            }
        }
    }
}
