using System;

namespace MassiveJobs.Core.DependencyInjection
{
    public interface IJobServiceScopeFactory: IDisposable
    {
        IJobServiceScope CreateScope();
    }
}