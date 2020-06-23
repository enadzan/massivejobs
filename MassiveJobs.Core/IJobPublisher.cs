using System;
using System.Collections.Generic;

namespace MassiveJobs.Core
{
    public interface IJobPublisher : IDisposable
    {
        void Publish(IEnumerable<JobInfo> jobs);

        void StartJobWorkers();
        void StopJobWorkers();
    }
}
