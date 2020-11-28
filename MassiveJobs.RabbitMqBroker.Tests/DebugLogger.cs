using System;
using System.Diagnostics;
using MassiveJobs.Core;

namespace MassiveJobs.RabbitMqBroker.Tests
{
    public class DebugLogger<T>: IJobLogger<T>
    {
        public bool IsEnabled(JobLogLevel logLevel)
        {
            return true;
        }

        public void Log(JobLogLevel logLevel, Exception exception, string message)
        {
            Debug.WriteLine(message);
        }
    }

    public class DebugLoggerFactory : IJobLoggerFactory
    {
        public IJobLogger<TCategory> CreateLogger<TCategory>()
        {
            return new DebugLogger<TCategory>();
        }
    }
}
