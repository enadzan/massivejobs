using System;
using NLog;
using MassiveJobs.Core;

namespace MassiveJobs.Logging.NLog
{
    public class LoggerWrapper : IJobLogger
    {
        private readonly ILogger _logger;

        public LoggerWrapper(ILogger logger)
        {
            _logger = logger;
        }

        public bool IsEnabled(JobLogLevel logLevel)
        {
            switch (logLevel)
            {
                case JobLogLevel.Trace: return _logger.IsTraceEnabled;
                case JobLogLevel.Debug: return _logger.IsDebugEnabled;
                case JobLogLevel.Information: return _logger.IsInfoEnabled;
                case JobLogLevel.Warning: return _logger.IsWarnEnabled;
                case JobLogLevel.Error: return _logger.IsErrorEnabled;
                case JobLogLevel.Critical: return _logger.IsFatalEnabled;
                default: return false;
            }
        }

        public void Log(JobLogLevel logLevel, Exception exception, string message)
        {
            switch (logLevel)
            {
                case JobLogLevel.Trace: _logger.Trace(exception, message); break;
                case JobLogLevel.Debug: _logger.Debug(exception, message); break;
                case JobLogLevel.Information: _logger.Info(exception, message); break;
                case JobLogLevel.Warning: _logger.Warn(exception, message); break;
                case JobLogLevel.Error: _logger.Error(exception, message); break;
                case JobLogLevel.Critical: _logger.Fatal(exception, message); break;
            }
        }
    }

    public class LoggerWrapperFactory : IJobLoggerFactory
    {
        public IJobLogger CreateLogger(string categoryName)
        {
            var logger = LogManager.GetLogger(categoryName);
            return new LoggerWrapper(logger);
        }
    }
}
