using System.Threading;
using MassiveJobs.Core.DependencyInjection;

namespace MassiveJobs.Core
{
    public interface IJobRunner
    {
        void RunJob(IJobPublisher publisher, JobInfo job, IJobServiceScope scope, CancellationToken cancellationToken);
    }
}