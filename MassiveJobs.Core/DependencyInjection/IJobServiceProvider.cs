using System;

namespace MassiveJobs.Core.DependencyInjection
{
    public interface IJobServiceProvider : IDisposable
    {
        object GetService(Type type);
        object GetRequiredService(Type type);
    }
}