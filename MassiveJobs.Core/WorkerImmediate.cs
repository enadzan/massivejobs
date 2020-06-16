using System;
using System.Collections.Generic;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

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

        protected override void ProcessMessageBatch(List<RawMessage> messages, IServiceScope serviceScope)
        {
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
                JobRunner.RunJobs(JobPublisher, batch, serviceScope, CancellationToken)
                    .Wait();
            }

            if (lastDeliveryTag.HasValue)
            {
                OnBatchProcessed(lastDeliveryTag.Value);
            }
        }
    }
}
