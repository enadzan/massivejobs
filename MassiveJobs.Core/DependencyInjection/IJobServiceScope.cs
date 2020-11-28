using System;

namespace MassiveJobs.Core.DependencyInjection
{
    public interface IJobServiceScope : IJobServiceFactory, IDisposable
    {
    }
}