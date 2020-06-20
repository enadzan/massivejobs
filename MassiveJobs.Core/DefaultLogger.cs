using System;

namespace MassiveJobs.Core
{
    class DefaultLogger<TLoggerCategory> : ILogger<TLoggerCategory>
    {
        public bool IsEnabled(LogLevel logLevel)
        {
            return false;
        }

        public void Log(LogLevel logLevel, Exception exception, string message)
        {
        }
    }

    public static class LoggerFactoryExtensions
    {
        public static ILogger<TCategoryName> SafeCreateLogger<TCategoryName>(this ILoggerFactory loggerFactory)
        {
            return loggerFactory != null ? loggerFactory.CreateLogger<TCategoryName>() : new DefaultLogger<TCategoryName>();
        }
    }
}
