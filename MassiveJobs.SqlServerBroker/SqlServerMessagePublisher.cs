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
        private readonly ISqlDialect _sqlDialect;
        private readonly ITimeProvider _timeProvider;

        public SqlServerMessagePublisher(TDbContext dbContext, ISqlDialect sqlDialect, ITimeProvider timeProvider)
        {
            _dbContext = dbContext;
            _sqlDialect = sqlDialect;
            _timeProvider = timeProvider;
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

                _sqlDialect.MessageQueueInsert(_dbContext, routingKey, msg.TypeTag, messageData, _timeProvider.GetCurrentTimeUtc());
            }
        }
    }
}
