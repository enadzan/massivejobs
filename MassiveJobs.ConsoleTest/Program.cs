using System;

using Microsoft.Extensions.Logging;
using LogLevel = Microsoft.Extensions.Logging.LogLevel;

using MassiveJobs.Core;
using MassiveJobs.RabbitMqBroker;
using System.Threading;

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

            RabbitMqJobsBroker.Initialize(
                true,
                mqSettings,
                s =>
                {
                    s.MaxQueueLength = QueueLength.NoLimit;
                    s.PublishBatchSize = 400;
                },
                new LoggerFactoryWrapper(loggerFactory)
            );

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
