namespace MassiveJobs.Core.Memory
{
    public static class JobsExtensions
    {
        public static Jobs WithInMemoryBroker(this Jobs jobs, InMemoryMessages messages = null)
        {
            jobs.RegisterInstance(messages ?? new InMemoryMessages());
            jobs.RegisterSingleton<IMessagePublisher, InMemoryMessagePublisher>();
            jobs.RegisterSingleton<IMessageConsumer, InMemoryMessageConsumer>();

            return jobs;
        }
    }
}
