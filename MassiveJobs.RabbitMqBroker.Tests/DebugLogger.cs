using System;
using System.Diagnostics;
using MassiveJobs.Core;

namespace MassiveJobs.RabbitMqBroker.Tests
{
    public class DebugLogger: IJobLogger
    {
        private readonly string _categoryName;

        public DebugLogger(string categoryName)
        {
            _categoryName = categoryName;
        }

        public bool IsEnabled(JobLogLevel logLevel)
        {
            return logLevel >= JobLogLevel.Debug;
        }

        public void Log(JobLogLevel logLevel, Exception exception, string message)
        {
            Debug.WriteLine($"{logLevel}: {_categoryName}");
            Debug.WriteLine(message);

            if (exception != null)
            {
                Debug.WriteLine(exception.StackTrace);
            }
        }
    }

    public class DebugLoggerFactory : IJobLoggerFactory
    {
        public IJobLogger CreateLogger(string categoryName)
        {
            return new DebugLogger(categoryName);
        }
    }
}
