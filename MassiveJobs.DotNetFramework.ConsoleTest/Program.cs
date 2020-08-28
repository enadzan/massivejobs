using System;

using Serilog;

using MassiveJobs.Core;
using MassiveJobs.RabbitMqBroker;

using MassiveJobs.Examples.Jobs;

namespace MassiveJobs.DotNetFramework.ConsoleTest
{
    internal class Program
    {
        private static void Main()
        {
            var mqSettings = new RabbitMqSettings
            {
                VirtualHost = "massivejobs",
                NamePrefix = "examples."
            };


            // initialize Serilog

            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Warning()
                .WriteTo.Console()
                .CreateLogger();

            // initialize RabbitMqJobs

            RabbitMqJobs.Initialize(true, mqSettings, s =>
            {
                s.MassiveJobs.MaxQueueLength = QueueLength.NoLimit;
                s.MassiveJobs.PublishBatchSize = 400;
                s.JobLoggerFactory = new MassiveJobs.Logging.Serilog.LoggerWrapperFactory();
            });

            Console.WriteLine("Testing periodic jobs. Press Enter to quit!");

            PeriodicJob.PublishPeriodic("test_periodic", "0/2 * * ? * *");

            Console.ReadLine();

            MassiveJobsMediator.DefaultInstance.Dispose();
        }
    }
}
