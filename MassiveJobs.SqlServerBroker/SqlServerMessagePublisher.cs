using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.EntityFrameworkCore;

using MassiveJobs.Core;

namespace MassiveJobs.SqlServerBroker
{
    public class SqlServerMessagePublisher<TDbContext> : IMessagePublisher where TDbContext: DbContext
    {
        private readonly TDbContext _dbContext;

        public SqlServerMessagePublisher(TDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public void Dispose()
        {
        }

        public void Publish(string routingKey, IEnumerable<RawMessage> messages, TimeSpan timeout)
        {
            foreach (var msg in messages)
            {
                // Limitation of this publisher - messages must be representable as UTF8 string

                var messageData = Encoding.UTF8.GetString(msg.Body);

                _dbContext.Database.ExecuteSqlInterpolated($@"
INSERT INTO massive_jobs.message_queue (routing_key, message_type, message_data, published_at_utc)
VALUES ({routingKey}, {msg.TypeTag}, {messageData}, {DateTime.UtcNow})
");

            }
        }
    }
}
