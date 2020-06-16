using Microsoft.Extensions.Logging;
using System;

namespace MassiveJobs.Core
{
    class DefaultLogger<TLoggerCategory> : ILogger<TLoggerCategory>, IDisposable
    {
        public IDisposable BeginScope<TState>(TState state)
        {
            return this;
        }

        public void Dispose()
        {
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return false;
        }

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
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
