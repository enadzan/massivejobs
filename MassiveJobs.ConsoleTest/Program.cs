using System;

using Microsoft.Extensions.Logging;

using MassiveJobs.Core;
using MassiveJobs.RabbitMqBroker;

namespace MassiveJobs.ConsoleTest
{
    class Program
    {
        static void Main(string[] args)
        {
            var loggerFactory = LoggerFactory.Create(builder =>
            {
                builder
                    .SetMinimumLevel(LogLevel.Warning)
                    .AddFilter("Microsoft", LogLevel.Warning)
                    .AddFilter("System", LogLevel.Warning)
                    .AddConsole();
            });

            var mqSettings = new RabbitMqSettings
            {
                VirtualHost = "massivejobs",
                NamePrefix = "console."
            };

            RabbitMqJobs.Initialize(true, mqSettings, s =>
            {
                s.MassiveJobs.MaxQueueLength = QueueLength.NoLimit;
                s.MassiveJobs.PublishBatchSize = 400;
                s.JobLoggerFactory = new LoggerFactoryWrapper(loggerFactory);
            });

            Console.WriteLine("Testing periodic jobs. Press Enter to quit!");

            PeriodicJob.PublishPeriodic("test_periodic", "0/2 * * ? * *", null);

            Console.ReadLine();

            MassiveJobsMediator.DefaultInstance.Dispose();
        }

        private class PeriodicJob: Job<PeriodicJob>
        {
            public override void Perform()
            {
                Console.WriteLine(DateTime.Now);
            }
        }
    }
}
