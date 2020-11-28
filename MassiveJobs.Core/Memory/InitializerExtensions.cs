namespace MassiveJobs.Core.Memory
{
    public static class InitializerExtensions
    {
        public static Jobs WithInMemoryBroker(this Jobs initializer, InMemoryMessages messages = null)
        {
            messages = messages ?? new InMemoryMessages();

            initializer.WithMessageBroker(
                p => new InMemoryMessagePublisher(p.GetRequiredService<MassiveJobsSettings>(), messages),
                _ => new InMemoryMessageConsumer(messages)
            );

            return initializer;
        }
    }
}
