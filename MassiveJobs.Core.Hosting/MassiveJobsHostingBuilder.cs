using Microsoft.Extensions.DependencyInjection;

namespace MassiveJobs.Core.Hosting
{
    public class MassiveJobsHostingBuilder
    {
        public IServiceCollection ServiceCollection { get; }

        public MassiveJobsHostingBuilder(IServiceCollection serviceCollection)
        {
            ServiceCollection = serviceCollection;
        }
    }
}