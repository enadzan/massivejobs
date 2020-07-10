using System;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace MassiveJobs.Core.Hosting
{
    public class MassiveJobsBackgroundService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<MassiveJobsBackgroundService> _logger;

        public MassiveJobsBackgroundService(IServiceScopeFactory scopeFactory, ILogger<MassiveJobsBackgroundService> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                if (!MassiveJobsMediator.IsInitialized)
                {
                    try
                    {
                        _logger.LogInformation("Initializing MassiveJobs");
                        MassiveJobsMediator.Initialize(new ServiceScopeFactoryWrapper(_scopeFactory));
                        MassiveJobsMediator.DefaultInstance.StartJobWorkers();
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "MassiveJobs initialization failed");
                    }
                }

                try
                {
                    await Task.Delay(10 * 1000, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                }
            }

            if (MassiveJobsMediator.IsInitialized)
            {
                try
                {
                    _logger.LogInformation("Deinitializing MassiveJobs");
                    MassiveJobsMediator.Deinitialize();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "MassiveJobs deinitialization failed");
                }
            }
        }
    }
}
