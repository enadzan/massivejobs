using System;

namespace MassiveJobs.Core
{
    public interface IJobServiceProvider : IDisposable
    {
        object GetService(Type type);
        object GetRequiredService(Type type);
    }

    public interface IJobServiceScope: IJobServiceProvider
    {
    }

    public interface IJobServiceScopeFactory: IDisposable
    {
        IJobServiceScope CreateScope();
    }
}
