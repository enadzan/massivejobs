using System;

namespace MassiveJobs.Core
{
    class DefaultLoggerFactory : IJobLoggerFactory
    {
        private readonly IJobLogger _defaultLogger = new DefaultJobLogger();

        public IJobLogger CreateLogger(string categoryName)
        {
            return _defaultLogger;
        }
    }

    class DefaultJobLogger : IJobLogger
    {
        public bool IsEnabled(JobLogLevel logLevel)
        {
            return false;
        }

        public void Log(JobLogLevel logLevel, Exception exception, string message)
        {
        }
    }

    class DefaultLogger<TCategory> : IJobLogger<TCategory>
    {
        private readonly IJobLogger _logger;

        public DefaultLogger(IJobLoggerFactory factory)
        {
            _logger = factory.CreateLogger(typeof(TCategory).FullName);
        }

        public bool IsEnabled(JobLogLevel logLevel)
        {
            return _logger.IsEnabled(logLevel);
        }

        public void Log(JobLogLevel logLevel, Exception exception, string message)
        {
            _logger.Log(logLevel, exception, message);
        }
    }
}
