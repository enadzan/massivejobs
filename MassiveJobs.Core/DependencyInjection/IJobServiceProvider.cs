using System;

namespace MassiveJobs.Core.DependencyInjection
{
    public interface IJobServiceProvider : IJobServiceFactory, IDisposable
    {
    }
}