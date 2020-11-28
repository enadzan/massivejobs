namespace MassiveJobs.Core.Memory
{
    public static class InitializerExtensions
    {
        public static Jobs WithInMemoryBroker(this Jobs initializer, InMemoryMessages messages = null)
        {
            messages = messages ?? new InMemoryMessages();

            initializer.WithMessagePublisher(new InMemoryMessagePublisher(initializer.Settings, messages));
            initializer.WithMessageConsumer(new InMemoryMessageConsumer(messages));

            return initializer;
        }
    }
}
