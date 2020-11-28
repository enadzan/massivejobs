using System;
using MassiveJobs.Core;

namespace MassiveJobs.Examples.Jobs
{
    public class PeriodicJob: Job<PeriodicJob>
    {
        public override void Perform()
        {
            Console.WriteLine(DateTime.Now);
        }
    }
}