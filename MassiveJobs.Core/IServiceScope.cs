using System;

namespace MassiveJobs.Core
{
    public interface IServiceScope: IDisposable
    {
        object GetService(Type type);
    }

    public interface IServiceScopeFactory
    {
        IServiceScope CreateScope();
    }
}
