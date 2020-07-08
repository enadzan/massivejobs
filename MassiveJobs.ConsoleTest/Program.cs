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

            RabbitMqJobsBroker.Initialize(true, mqSettings, s =>
            {
                s.LoggerFactory = new LoggerFactoryWrapper(loggerFactory);
                s.MaxQueueLength = QueueLength.NoLimit;
                s.PublishBatchSize = 400;
            });

            Console.WriteLine("Testing periodic jobs. Press Enter to quit!");

            PeriodicJob.PerformPeriodic("test_periodic", "0/2 * * ? * *", null);

            Console.ReadLine();

            MassiveJobsMediator.DefaultInstance.Dispose();
        }

        private class PeriodicJob: Job<PeriodicJob>
        {
            public override void Perform(CancellationToken cancellationToken)
            {
                Console.WriteLine(DateTime.Now);
            }
        }
    }
}
