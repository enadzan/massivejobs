using MassiveJobs.Core;
using Microsoft.EntityFrameworkCore;

namespace MassiveJobs.SqlServerBroker
{
    public static class JobsBuilderExtensions
    {
        public static JobsBuilder WithSqlServerBroker<TDbContext>(this JobsBuilder builder) where TDbContext: DbContext
        {
            builder.RegisterScoped<IMessagePublisher, SqlServerMessagePublisher<TDbContext>>();
            builder.RegisterSingleton<IMessageConsumer, SqlServerMessageConsumer<TDbContext>>();
            builder.RegisterSingleton<ISqlDialect, SqlDialects.SqlServerDialect>();

            return builder;
        }
    }
}
