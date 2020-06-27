using System;

namespace MassiveJobs.Core
{
    class DefaultServiceScope : IServiceScope
    {
        public object GetService(Type serviceType)
        {
            return null;
        }

        public void Dispose()
        {
        }
    }

    public static class ServiceScopeExtensions
    {
        public static IServiceScope SafeCreateScope(this IServiceScopeFactory serviceScopeFactory)
        {
            return serviceScopeFactory == null ? new DefaultServiceScope() : serviceScopeFactory.CreateScope();
        }

        public static TService GetService<TService>(this IServiceScope scope)
        {
            return (TService)scope?.GetService(typeof(TService));
        }
    }
}
