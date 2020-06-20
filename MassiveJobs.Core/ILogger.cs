using System;

namespace MassiveJobs.Core
{
    public enum LogLevel
    {
        Trace = 0,
        Debug = 1,
        Information = 2,
        Warning = 3,
        Error = 4,
        Critical = 5,
        None = 6
    }

    public interface ILogger
    {
        bool IsEnabled(LogLevel logLevel);
        void Log(LogLevel logLevel, Exception exception, string message);
    }

    public interface ILogger<out TCategory>: ILogger
    {
    }

    public interface ILoggerFactory
    {
        ILogger<TCategory> CreateLogger<TCategory>();
    }
}
