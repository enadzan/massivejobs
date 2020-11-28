﻿using System;

using Microsoft.Extensions.Logging;

using MassiveJobs.Core;
using MassiveJobs.RabbitMqBroker;

using MassiveJobs.Examples.Jobs;

namespace MassiveJobs.DotNetCore.ConsoleTest
{
    internal class Program
    {
        private static void Main()
        {
            var loggerFactory = LoggerFactory.Create(builder =>
            {
                builder
                    .SetMinimumLevel(LogLevel.Warning)
                    .AddFilter("Microsoft", LogLevel.Warning)
                    .AddFilter("System", LogLevel.Warning)
                    .AddConsole();
            });

            RabbitMqJobs.Initialize(true, s =>
            {
                s.RabbitMqSettings.VirtualHost = "massivejobs";
                s.RabbitMqSettings.NamePrefix = "examples.";
                s.MaxQueueLength = QueueLength.NoLimit;
                s.PublishBatchSize = 400;
                s.JobLoggerFactory = new LoggerFactoryWrapper(loggerFactory);
            });

            Console.WriteLine("Testing periodic jobs. Press Enter to quit!");

            PeriodicJob.PublishPeriodic("test_periodic", "0/2 * * ? * *");

            Console.ReadLine();

            MassiveJobsMediator.DefaultInstance.Dispose();
        }
    }
}
