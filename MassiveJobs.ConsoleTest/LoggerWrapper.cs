using System;
using Microsoft.Extensions.Logging;

namespace MassiveJobs.ConsoleTest
{
    class LoggerWrapper<TCategory> : Core.ILogger<TCategory>
    {
        private readonly ILogger _msLogger;

        public LoggerWrapper(ILogger<TCategory> msLogger)
        {
            _msLogger = msLogger;
        }

        public bool IsEnabled(Core.LogLevel logLevel)
        {
            return _msLogger.IsEnabled(ConvertLogLevel(logLevel));
        }

        public void Log(Core.LogLevel logLevel, Exception exception, string message)
        {
            _msLogger.Log(ConvertLogLevel(logLevel), exception, message);
        }

        private LogLevel ConvertLogLevel(Core.LogLevel logLevel)
        {
            switch (logLevel)
            {
                case Core.LogLevel.Trace:
                    return LogLevel.Trace;
                case Core.LogLevel.Debug:
                    return LogLevel.Debug;
                case Core.LogLevel.Information:
                    return LogLevel.Information;
                case Core.LogLevel.Warning:
                    return LogLevel.Warning;
                case Core.LogLevel.Error:
                    return LogLevel.Error;
                case Core.LogLevel.Critical:
                    return LogLevel.Critical;
                default:
                    return LogLevel.None;
            }
        }
    }
}
