using System;
using MassiveJobs.Core;
using Microsoft.EntityFrameworkCore;

namespace MassiveJobs.SqlServerBroker
{
    public static class JobsBuilderExtensions
    {
        public static JobsBuilder WithSqlServerBroker<TDbContext>(this JobsBuilder builder, Action<SqlServerBrokerOptions> configureAction = null) where TDbContext: DbContext
        {
            var options = new SqlServerBrokerOptions();

            configureAction?.Invoke(options);

            builder.RegisterInstance(options);
            builder.RegisterScoped<IMessagePublisher, SqlServerMessagePublisher<TDbContext>>();
            builder.RegisterSingleton<IMessageConsumer, SqlServerMessageConsumer<TDbContext>>();
            builder.RegisterSingleton<ISqlDialect, SqlDialects.SqlServerDialect>();

            return builder;
        }
    }
}
