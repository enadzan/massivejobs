using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore;

using MassiveJobs.Core;

namespace MassiveJobs.SqlServerBroker.SqlDialects
{
    public class SqlServerDialect : ISqlDialect
    {
        private readonly IJobLogger<SqlServerDialect> _logger;

        public SqlServerDialect(IJobLogger<SqlServerDialect> logger)
        {
            _logger = logger;
        }

        public int MessageQueueInsert(DbContext dbContext, string routingKey, string argsType, string messageData, DateTime publishedAtUtc)
        {
            try
            {
                return dbContext.Database.ExecuteSqlInterpolated($@"
INSERT INTO massive_jobs.message_queue (routing_key, args_type, message_data, published_at_utc)
VALUES ({routingKey}, {argsType}, {messageData}, {publishedAtUtc})
");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed db access");
                throw;
            }
        }

        public int MessageQueueAckProcessed(DbContext dbContext, string processingInstance, DateTime processingTimeUtc)
        {
            try
            {
                return  dbContext.Database.ExecuteSqlInterpolated($@"
UPDATE massive_jobs.message_queue 
SET processing_end_utc = {processingTimeUtc} 
WHERE processing_end_utc IS NULL
    AND processing_instance = {processingInstance}
");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed db access");
                throw;
            }
        }

        public int MessageQueueKeepalive(DbContext dbContext, string instanceName, DateTime utcNow)
        {
            try
            {
                return dbContext.Database.ExecuteSqlInterpolated($@"
UPDATE massive_jobs.message_queue 
SET processing_keepalive_utc = {utcNow} 
WHERE processing_end_utc IS NULL
    AND processing_instance = {instanceName}
");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed db access");
                throw;
            }
        }

        public List<MessageQueue> MessageQueueGetNextBatch(DbContext dbContext, string instanceName, string routingKey, DateTime utcNow, int batchSize)
        {
            try
            {
                return dbContext.Set<MessageQueue>().FromSqlInterpolated($@"
UPDATE TOP ({batchSize}) massive_jobs.message_queue
SET processing_start_utc = {utcNow}, processing_keepalive_utc = {utcNow}, processing_instance = {instanceName}
OUTPUT inserted.*
WHERE processing_end_utc IS NULL
    AND routing_key = {routingKey}
    AND (processing_keepalive_utc IS NULL OR processing_keepalive_utc < {utcNow.AddSeconds(-20)})")
                .ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed db access");
                throw;
            }
        }

        public int MessageQueueRelease(DbContext dbContext, string instanceName, string routingKey)
        {
            try
            {
                return dbContext.Database.ExecuteSqlInterpolated($@"
UPDATE massive_jobs.message_queue
SET processing_start_utc = NULL, processing_keepalive_utc = NULL, processing_instance = NULL
WHERE processing_end_utc IS NULL
    AND routing_key = {routingKey}
    AND processing_instance = {instanceName}
");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed db access");
                throw;
            }
        }

        public int SingleConsumerLockUpdate(DbContext dbContext, string instanceName, string routingKey, DateTime utcNow)
        {
            try
            {
                return dbContext.Database.ExecuteSqlInterpolated($@"
UPDATE massive_jobs.single_consumer_lock 
SET lock_keepalive_utc = {utcNow}, instance_name = {instanceName}
WHERE routing_key = {routingKey}
    AND (lock_keepalive_utc IS NULL OR lock_keepalive_utc < {utcNow.AddSeconds(-20)} OR instance_name = {instanceName})
");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed db access");
                throw;
            }
        }

        public int SingleConsumerLockRelease(DbContext dbContext, string instanceName, string routingKey)
        {
            try
            {
                return dbContext.Database.ExecuteSqlInterpolated($@"
UPDATE massive_jobs.single_consumer_lock 
SET lock_keepalive_utc = null, instance_name = null
WHERE routing_key = {routingKey}
    AND instance_name = {instanceName}
");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed db access");
                throw;
            }
        }
    }
}
