using System;

namespace MassiveJobs.Core.DependencyInjection
{
    public interface IJobServiceProvider : IDisposable
    {
        IJobServiceFactory ServiceFactory { get; }
        IJobServiceCollection ServiceCollection { get; }
    }
}