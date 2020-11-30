using System;

using Serilog;

using MassiveJobs.Core;
using MassiveJobs.Logging.Serilog;
using MassiveJobs.RabbitMqBroker;

using MassiveJobs.Examples.Jobs;

namespace MassiveJobs.DotNetFramework.ConsoleTest
{
    internal class Program
    {
        private static void Main()
        {
            // initialize Serilog

            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Warning()
                .WriteTo.Console()
                .CreateLogger();

            // initialize MassiveJobs

            JobsBuilder.Configure()
                .WithSerilog()
                .WithSettings("examples.")
                .WithRabbitMqBroker()
                .Build();

            Console.WriteLine("Testing periodic jobs. Press Enter to quit!");

            PeriodicJob.PublishPeriodic("test_periodic", "0/2 * * ? * *");

            Console.ReadLine();

            JobsBuilder.DisposeJobs();
        }
    }
}
