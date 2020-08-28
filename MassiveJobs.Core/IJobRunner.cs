using System.Threading;

namespace MassiveJobs.Core
{
    public interface IJobRunner
    {
        void RunJob(IJobPublisher publisher, JobInfo job, IJobServiceScope scope, CancellationToken cancellationToken);
    }
}