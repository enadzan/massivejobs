namespace MassiveJobs.Core.Memory
{
    public static class JobsBuilderExtensions
    {
        public static JobsBuilder WithInMemoryBroker(this JobsBuilder jobs, InMemoryMessages messages = null)
        {
            jobs.RegisterInstance(messages ?? new InMemoryMessages());
            jobs.RegisterSingleton<IMessagePublisher, InMemoryMessagePublisher>();
            jobs.RegisterSingleton<IMessageConsumer, InMemoryMessageConsumer>();

            return jobs;
        }
    }
}
