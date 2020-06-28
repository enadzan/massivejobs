using System;

namespace MassiveJobs.Core
{
    public interface IServiceCollection: IDisposable
    {
        void AddSingleton(Type serviceType, object instance);
    }

    public interface IServiceScope: IDisposable
    {
        object GetService(Type type);
    }

    public interface IServiceScopeFactory: IDisposable
    {
        IServiceCollection ServiceCollection { get; }
        IServiceScope CreateScope();
    }
}
