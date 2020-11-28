using System;
using MassiveJobs.Core.LightInject;

namespace MassiveJobs.Core.DependencyInjection
{
    class DefaultJobServiceScope : IJobServiceScope
    {
        private readonly Scope _scope;

        internal DefaultJobServiceScope(Scope scope)
        {
            _scope = scope;
        }

        public object GetRequiredService(Type serviceType)
        {
            var svc = GetService(serviceType);
            return svc ?? throw new ArgumentException($"Service of type {serviceType?.AssemblyQualifiedName} is not registered.");
        }

        public virtual object GetService(Type serviceType)
        {
            return _scope.TryGetInstance(serviceType);
        }

        public void Dispose()
        {
            _scope.Dispose();
        }
    }
}
