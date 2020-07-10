using System;

namespace MassiveJobs.Core
{
    class DefaultLogger<TLoggerCategory> : IJobLogger<TLoggerCategory>
    {
        public bool IsEnabled(JobLogLevel logLevel)
        {
            return false;
        }

        public void Log(JobLogLevel logLevel, Exception exception, string message)
        {
        }
    }

    public static class LoggerFactoryExtensions
    {
        public static IJobLogger<TCategoryName> SafeCreateLogger<TCategoryName>(this IJobLoggerFactory loggerFactory)
        {
            return loggerFactory != null ? loggerFactory.CreateLogger<TCategoryName>() : new DefaultLogger<TCategoryName>();
        }
    }
}
