namespace MassiveJobs.Core.DependencyInjection
{
    public static class JobServiceFactoryExtensions
    {
        public static TService GetService<TService>(this IJobServiceFactory factory)
        {
            return (TService)factory.GetService(typeof(TService));
        }

        public static TService GetRequiredService<TService>(this IJobServiceFactory factory)
        {
            return (TService)factory.GetRequiredService(typeof(TService));
        }
    }
}