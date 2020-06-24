using System;
using System.Collections.Generic;
using System.Threading;

namespace MassiveJobs.Core
{
    public class WorkerImmediate : Worker
    {
        public WorkerImmediate(
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
        }

        protected override void ProcessMessageBatch(List<RawMessage> messages, IServiceScope serviceScope, CancellationToken cancellationToken, out int pauseSec)
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

            if (batch.Count > 0)
            {
                JobRunner.RunJobs(JobPublisher, batch, serviceScope, cancellationToken);
            }

            if (cancellationToken.IsCancellationRequested) return;

            if (lastDeliveryTag.HasValue)
            {
                OnBatchProcessed(lastDeliveryTag.Value);
            }
        }
    }
}
