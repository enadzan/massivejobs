using Microsoft.Extensions.DependencyInjection;
using System;

namespace MassiveJobs.Core
{
    class DefaultServiceScope : IServiceScope
    {
        public IServiceProvider ServiceProvider { get; }

        public DefaultServiceScope()
        {
            ServiceProvider = new DefaultServiceProvider();
        }

        public void Dispose()
        {
        }
    }

    class DefaultServiceProvider : IServiceProvider
    {
        public object GetService(Type serviceType)
        {
            return null;
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
