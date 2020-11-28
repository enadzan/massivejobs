using MassiveJobs.Core.Memory;
using Microsoft.Extensions.DependencyInjection;

namespace MassiveJobs.Core.Hosting
{
    public static class MassiveJobsHostingBuilderExtensions
    {
        public static void UseInMemoryBroker(this MassiveJobsHostingBuilder hostingBuilder)
        {
            hostingBuilder.ServiceCollection.AddSingleton(new InMemoryMessages());
            hostingBuilder.ServiceCollection.AddSingleton<IMessagePublisher, InMemoryMessagePublisher>();
            hostingBuilder.ServiceCollection.AddSingleton<IMessageConsumer, InMemoryMessageConsumer>();
        }
    }
}