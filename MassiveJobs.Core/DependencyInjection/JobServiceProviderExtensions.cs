namespace MassiveJobs.Core.DependencyInjection
{
    public static class JobServiceProviderExtensions
    {
        public static TService GetService<TService>(this IJobServiceProvider provider)
        {
            return (TService)provider?.GetService(typeof(TService));
        }

        public static TService GetRequiredService<TService>(this IJobServiceProvider provider)
        {
            return (TService)provider?.GetRequiredService(typeof(TService));
        }
    }
}