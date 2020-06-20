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

    public static class ServiceScopeFactoryExtensions
    {
        public static IServiceScope SafeCreateScope(this IServiceScopeFactory scopeFactory)
        {
            return scopeFactory == null ? new DefaultServiceScope() : scopeFactory.CreateScope();
        }
    }
}
