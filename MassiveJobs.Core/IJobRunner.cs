using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace MassiveJobs.Core
{
    public interface IJobRunner
    {
        void RunJobs(IJobPublisher publisher, IEnumerable<JobInfo> jobs, IServiceScope serviceScope, CancellationToken cancellationToken);
    }
}