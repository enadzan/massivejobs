using System;
using System.Collections.Generic;

using Microsoft.EntityFrameworkCore;

namespace MassiveJobs.SqlServerBroker
{
    public interface ISqlDialect
    {
        int MessageQueueInsert(DbContext dbContext, string routingKey, string argsType, string messageData, DateTime publishedAtUtc);
        int MessageQueueAckProcessed(DbContext dbContext, string processingInstance, DateTime processingTimeUtc);
        int MessageQueueKeepAlive(DbContext dbContext, string instanceName, DateTime utcNow);
        List<MessageQueue> MessageQueueGetNextBatch(DbContext dbContext, string instanceName, string routingKey, DateTime utcNow, int batchSize);
        int MessageQueueRelease(DbContext dbContext, string instanceName, string routingKey);
        int SingleConsumerLockUpdate(DbContext dbContext, string instanceName, string routingKey, DateTime utcNow);
        int SingleConsumerLockRelease(DbContext dbContext, string instanceName, string routingKey);
    }
}
