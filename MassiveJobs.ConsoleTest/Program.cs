using System;

using Microsoft.Extensions.Logging;
using LogLevel = Microsoft.Extensions.Logging.LogLevel;

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

            using var jobs = RabbitMqJobsBuilder
                    .FromSettings(mqSettings)
                    .Configure(s =>
                    {
                        s.LoggerFactory = new LoggerFactoryWrapper(loggerFactory); 
                        s.MaxQueueLength = QueueLength.NoLimit;
                        s.PublishBatchSize = 400;
                    })
                    .Build();

            jobs.StartJobWorkers();

            Console.WriteLine("Testing periodic jobs. Press Enter to quit!");

            jobs.PublishPeriodic<PeriodicJob>("test_periodic", "0/2 * * ? * *", null);

            Console.ReadLine();
        }

        private class PeriodicJob
        {
            public void Perform()
            {
                Console.WriteLine(DateTime.Now);
            }
        }
    }
}
