using System;

namespace MassiveJobs.Core.DependencyInjection
{
    public interface IJobServiceFactory
    {
        object GetService(Type serviceType);
        object GetRequiredService(Type serviceType);
    }
}
