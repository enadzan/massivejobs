using System;
using MassiveJobs.Core.LightInject;

namespace MassiveJobs.Core.DependencyInjection
{
    class DefaultServiceFactory : IJobServiceFactory
    {
        private readonly IServiceFactory _serviceFactory;

        internal DefaultServiceFactory(IServiceFactory serviceFactory)
        {
            _serviceFactory = serviceFactory;
        }

        public object GetService(Type type)
        {
            return _serviceFactory.TryGetInstance(type);
        }

        public object GetRequiredService(Type type)
        {
            return _serviceFactory.GetInstance(type);
        }
    }
}