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
                HostNames = new[] { "localhost" },
                VirtualHost = "massivejobs",
                Username = "guest",
                Password = "guest",
                NamePrefix = "console."
            };

            using var scopeFactory = RabbitMqBrokerBuilder
                    .FromSettings(mqSettings)
                    .Configure(s =>
                    {
                        s.LoggerFactory = new LoggerFactoryWrapper(loggerFactory); 
                        s.MaxQueueLength = QueueLength.NoLimit;
                        s.PublishBatchSize = 400;
                    })
                    .GetScopeFactory();

            using var scope = scopeFactory.CreateScope();

            var workers = scope.GetService<IWorkerCoordinator>();

            workers.StartJobWorkers();

            var publisher = scope.GetService<IJobPublisher>();

            Console.WriteLine("Testing periodic jobs. Press Enter to quit!");

            publisher.PublishPeriodic<PeriodicJob>("test_periodic", 2, DateTime.UtcNow.Date);

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
