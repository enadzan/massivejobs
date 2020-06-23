using System.Collections.Generic;
using System.Threading;

namespace MassiveJobs.Core
{
    public interface IJobRunner
    {
        void RunJobs(IJobPublisher publisher, IEnumerable<JobInfo> jobs, IServiceScope serviceScope, CancellationToken cancellationToken);
    }
}