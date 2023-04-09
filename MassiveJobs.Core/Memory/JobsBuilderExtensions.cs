using Microsoft.Extensions.DependencyInjection;

namespace MassiveJobs.Core.Memory
{
    public static class JobsBuilderExtensions
    {
        public static JobsBuilder WithInMemoryBroker(this JobsBuilder jobs, InMemoryMessages messages = null)
        {
            jobs.ServiceCollection.AddSingleton(messages ?? new InMemoryMessages());
            jobs.ServiceCollection.AddSingleton<IMessagePublisher, InMemoryMessagePublisher>();
            jobs.ServiceCollection.AddSingleton<IMessageConsumer, InMemoryMessageConsumer>();

            return jobs;
        }
    }
}
