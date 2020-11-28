using System;

namespace MassiveJobs.Core
{
    public enum JobLogLevel
    {
        Trace = 0,
        Debug = 1,
        Information = 2,
        Warning = 3,
        Error = 4,
        Critical = 5,
        None = 6
    }

    public interface IJobLogger
    {
        bool IsEnabled(JobLogLevel logLevel);
        void Log(JobLogLevel logLevel, Exception exception, string message);
    }

    public interface IJobLogger<out TCategory>: IJobLogger
    {
    }

    public interface IJobLoggerFactory
    {
        IJobLogger CreateLogger(string categoryName);
    }
}
