using System;
using log4net;
using MassiveJobs.Core;

namespace MassiveJobs.Logging.Log4Net
{
    public class LoggerWrapper<TCategory> : IJobLogger<TCategory>
    {
        private ILog _logger;

        public LoggerWrapper(ILog logger)
        {
            _logger = logger;
        }

        public bool IsEnabled(JobLogLevel logLevel)
        {
            switch (logLevel)
            {
                case JobLogLevel.Trace: return false;
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
                case JobLogLevel.Trace: break;
                case JobLogLevel.Debug: _logger.Debug(message, exception); break;
                case JobLogLevel.Information: _logger.Info(message, exception); break;
                case JobLogLevel.Warning: _logger.Warn(message, exception); break;
                case JobLogLevel.Error: _logger.Error(message, exception); break;
                case JobLogLevel.Critical: _logger.Fatal(message, exception); break;
            }
        }
    }

    public class LoggerWrapperFactory : IJobLoggerFactory
    {
        public IJobLogger<TCategory> CreateLogger<TCategory>()
        {
            var logger = LogManager.GetLogger(typeof(TCategory));

            return new LoggerWrapper<TCategory>(logger);
        }
    }
}
