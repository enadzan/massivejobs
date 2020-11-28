using MassiveJobs.Core.LightInject;

namespace MassiveJobs.Core.DependencyInjection
{
    class DefaultServiceScopeFactory : IJobServiceScopeFactory
    {
        private readonly IServiceFactory _factory;

        public DefaultServiceScopeFactory(IServiceFactory factory)
        {
            _factory = factory;
        }

        public void Dispose()
        {
        }

        public IJobServiceScope CreateScope()
        {
            return new DefaultJobServiceScope(_factory.BeginScope());
        }
    }
}