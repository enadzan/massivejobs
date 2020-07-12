using System;
using Serilog;
using Serilog.Events;
using MassiveJobs.Core;

namespace MassiveJobs.Logging.Serilog
{
    public class LoggerWrapper<TCategory> : IJobLogger<TCategory>
    {
        private ILogger _logger;

        public LoggerWrapper(ILogger logger)
        {
            _logger = logger;
        }

        public bool IsEnabled(JobLogLevel logLevel)
        {
            switch (logLevel)
            {
                case JobLogLevel.Trace: return _logger.IsEnabled(LogEventLevel.Verbose);
                case JobLogLevel.Debug: return _logger.IsEnabled(LogEventLevel.Debug);
                case JobLogLevel.Information: return _logger.IsEnabled(LogEventLevel.Information);
                case JobLogLevel.Warning: return _logger.IsEnabled(LogEventLevel.Warning);
                case JobLogLevel.Error: return _logger.IsEnabled(LogEventLevel.Error);
                case JobLogLevel.Critical: return _logger.IsEnabled(LogEventLevel.Fatal);
                default: return false;
            }
        }

        public void Log(JobLogLevel logLevel, Exception exception, string message)
        {
            switch (logLevel)
            {
                case JobLogLevel.Trace: _logger.Verbose(exception, message); break;
                case JobLogLevel.Debug: _logger.Debug(exception, message); break;
                case JobLogLevel.Information: _logger.Information(exception, message); break;
                case JobLogLevel.Warning: _logger.Warning(exception, message); break;
                case JobLogLevel.Error: _logger.Error(exception, message); break;
                case JobLogLevel.Critical: _logger.Fatal(exception, message); break;
            }
        }
    }

    public class LoggerWrapperFactory : IJobLoggerFactory
    {
        public IJobLogger<TCategory> CreateLogger<TCategory>()
        {
            return new LoggerWrapper<TCategory>(Log.ForContext<TCategory>());
        }
    }
}
