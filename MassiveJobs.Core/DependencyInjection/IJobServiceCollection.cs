using System;

namespace MassiveJobs.Core.DependencyInjection
{
    public interface IJobServiceCollection
    {
        void RegisterInstance<TService>(Func<IJobServiceProvider, TService> factory);
        void Compile();
    }
}
