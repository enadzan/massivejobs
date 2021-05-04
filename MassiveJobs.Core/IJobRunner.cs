using System.Threading;
using MassiveJobs.Core.DependencyInjection;

namespace MassiveJobs.Core
{
    public interface IJobRunner
    {
        void RunJob(IJobPublisher publisher, IMessageReceiver receiver, JobInfo job, ulong deliveryTag, IJobServiceScope scope, CancellationToken cancellationToken);
    }
}