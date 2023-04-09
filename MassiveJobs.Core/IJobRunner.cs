using System.Threading;

using Microsoft.Extensions.DependencyInjection;

namespace MassiveJobs.Core
{
    public interface IJobRunner
    {
        void RunJob(IJobPublisher publisher, IMessageReceiver receiver, JobInfo job, ulong deliveryTag, IServiceScope scope, CancellationToken cancellationToken);
    }
}