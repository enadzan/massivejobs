using Microsoft.Extensions.DependencyInjection;
using System;

namespace MassiveJobs.Core.Hosting
{
    public class ServiceScopeWrapper : IJobServiceScope
    {
        private readonly IServiceScope _scope;

        public ServiceScopeWrapper(IServiceScope scope)
        {
            _scope = scope;
        }

        public void Dispose()
        {
            _scope.Dispose();
        }

        public object GetRequiredService(Type type)
        {
            return _scope.ServiceProvider.GetRequiredService(type);
        }

        public object GetService(Type type)
        {
            return _scope.ServiceProvider.GetService(type);
        }
    }

    public class ServiceScopeFactoryWrapper : IJobServiceScopeFactory
    {
        private readonly IServiceScopeFactory _scopeFactory;

        public ServiceScopeFactoryWrapper(IServiceScopeFactory factory)
        {
            _scopeFactory = factory;
        }

        public IJobServiceScope CreateScope()
        {
            return new ServiceScopeWrapper(_scopeFactory.CreateScope());
        }

        public void Dispose()
        {
        }
    }
}

