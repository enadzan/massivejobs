using Microsoft.EntityFrameworkCore;

using MassiveJobs.Core;
using MassiveJobs.Core.DependencyInjection;

namespace MassiveJobs.SqlServerBroker
{
    public class SqlServerMessageConsumer<TDbContext> : IMessageConsumer where TDbContext: DbContext
    {
        private readonly IJobServiceScopeFactory _serviceScopeFactory;
        private readonly IJobLoggerFactory _jobLoggerFactory;

        #pragma warning disable 0067
        public event MessageConsumerDisconnected Disconnected;
        #pragma warning restore 0067

        public SqlServerMessageConsumer(IJobServiceScopeFactory serviceScopeFactory, IJobLoggerFactory jobLoggerFactory)
        {
            _serviceScopeFactory = serviceScopeFactory;
            _jobLoggerFactory = jobLoggerFactory;
        }
        public void Connect()
        {
        }

        public IMessageReceiver CreateReceiver(string queueName, bool singleActiveConsumer = false)
        {
            return new SqlServerMessageReceiver<TDbContext>(queueName, singleActiveConsumer, _serviceScopeFactory, 
                _jobLoggerFactory.CreateLogger<SqlServerMessageReceiver<TDbContext>>());
        }

        public void Dispose()
        {
        }
    }
}
