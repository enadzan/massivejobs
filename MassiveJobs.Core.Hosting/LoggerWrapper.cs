using Microsoft.Extensions.Logging;
using System;

namespace MassiveJobs.Core.Hosting
{
    public class LoggerFactoryWrapper : IJobLoggerFactory
    {
        private readonly ILoggerFactory _msLoggerFactory;

        public LoggerFactoryWrapper(ILoggerFactory msLoggerFactory)
        {
            _msLoggerFactory = msLoggerFactory;
        }

        public IJobLogger CreateLogger(string categoryName)
        {
            return new LoggerWrapper(_msLoggerFactory.CreateLogger(categoryName));
        }
    }

    public class LoggerWrapper : IJobLogger
    {
        private readonly ILogger _msLogger;

        public LoggerWrapper(ILogger msLogger)
        {
            _msLogger = msLogger;
        }

        public bool IsEnabled(JobLogLevel logLevel)
        {
            return _msLogger.IsEnabled(ConvertLogLevel(logLevel));
        }

        public void Log(JobLogLevel logLevel, Exception exception, string message)
        {
            _msLogger.Log(ConvertLogLevel(logLevel), exception, message);
        }

        private LogLevel ConvertLogLevel(JobLogLevel logLevel)
        {
            switch (logLevel)
            {
                case JobLogLevel.Trace:
                    return LogLevel.Trace;
                case JobLogLevel.Debug:
                    return LogLevel.Debug;
                case JobLogLevel.Information:
                    return LogLevel.Information;
                case JobLogLevel.Warning:
                    return LogLevel.Warning;
                case JobLogLevel.Error:
                    return LogLevel.Error;
                case JobLogLevel.Critical:
                    return LogLevel.Critical;
                default:
                    return LogLevel.None;
            }
        }
    }

    public class LoggerWrapper<TCategory> : IJobLogger<TCategory>
    {
        private readonly LoggerWrapper _loggerWrapper;

        public LoggerWrapper(ILogger<TCategory> msLogger)
        {
            _loggerWrapper = new LoggerWrapper(msLogger);
        }

        public bool IsEnabled(JobLogLevel logLevel)
        {
            return _loggerWrapper.IsEnabled(logLevel);
        }

        public void Log(JobLogLevel logLevel, Exception exception, string message)
        {
            _loggerWrapper.Log(logLevel, exception, message);
        }
    }
}
