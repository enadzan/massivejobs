using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace MassiveJobs.Core.Hosting
{
    public static class HostExtensions
    {
        public static IHost InitMassiveJobs(this IHost host, Action postInitAction = null, bool startWorkers = true)
        {
            var logger = host.Services.GetService<ILogger>();

            try
            {
                logger?.LogInformation("Initializing MassiveJobs");

                var serviceScopeFactory = host.Services.GetRequiredService<IServiceScopeFactory>();

                MassiveJobsMediator.Initialize(new ServiceScopeFactoryWrapper(serviceScopeFactory));

                postInitAction?.Invoke();

                if (startWorkers)
                {
                    MassiveJobsMediator.DefaultInstance.StartJobWorkers();
                }
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, "MassiveJobs initialization failed");
            }

            return host;
        }
    }
}
