using System;

namespace MassiveJobs.Core.DependencyInjection
{
    public interface IJobServiceCollection
    {
        void RegisterInstance<TService>(TService instance);
        void RegisterSingleton<TService>(Func<IJobServiceFactory, TService> factory);
        void RegisterSingleton<TService, TImplementation>() where TImplementation : TService;
        void RegisterScoped<TService>(Func<IJobServiceFactory, TService> factory);
        void RegisterScoped<TService, TImplementation>() where TImplementation : TService;
        void RegisterTransient<TService>(Func<IJobServiceFactory, TService> factory);
        void RegisterTransient<TService, TImplementation>() where TImplementation : TService;

        void Validate();
        void Compile();
    }
}