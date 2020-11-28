using MassiveJobs.Core.Memory;
using Microsoft.Extensions.DependencyInjection;

namespace MassiveJobs.Core.Hosting
{
    public static class MassiveJobsHostingBuilderExtensions
    {
        public static void UseInMemoryBroker(this MassiveJobsHostingBuilder hostingBuilder)
        {
            var messages = new InMemoryMessages();

            hostingBuilder.ServiceCollection.AddSingleton(
                p => new InMemoryMessagePublisher(p.GetRequiredService<MassiveJobsSettings>(), messages));

            hostingBuilder.ServiceCollection.AddSingleton(new InMemoryMessageConsumer(messages));
        }
    }
}