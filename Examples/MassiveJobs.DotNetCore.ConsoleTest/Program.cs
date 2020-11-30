using System;
using Serilog;

using MassiveJobs.Core;
using MassiveJobs.Logging.Serilog;
using MassiveJobs.RabbitMqBroker;

using MassiveJobs.Examples.Jobs;

namespace MassiveJobs.DotNetCore.ConsoleTest
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

            // Initialize MassiveJobs

            JobsBuilder.Configure()
                .WithSettings("examples.")
                .WithRabbitMqBroker()
                .WithSerilog() // NOT needed if running in WorkerService or ASP.NET Core (more at: https://massivejobs.net)
                .Build();

            Console.WriteLine("Testing periodic jobs. Press Enter to quit!");

            PeriodicJob.PublishPeriodic("test_periodic", "0/2 * * ? * *");

            Console.ReadLine();

            JobsBuilder.DisposeJobs();
        }
    }
}
