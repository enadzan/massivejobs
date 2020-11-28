using System;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace MassiveJobs.Core.Hosting
{
    public class MassiveJobsBackgroundService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly MassiveJobsHostingOptions _options;
        private readonly ILogger<MassiveJobsBackgroundService> _logger;

        public MassiveJobsBackgroundService(IServiceProvider serviceProvider, MassiveJobsHostingOptions options,
            ILogger<MassiveJobsBackgroundService> logger)
        {
            _serviceProvider = serviceProvider;
            _options = options;
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

                        MassiveJobsMediator.Initialize(new ServiceProviderWrapper(_serviceProvider));

                        _options.OnInitAction?.Invoke();

                        if (_options.StartWorkers)
                        {
                            MassiveJobsMediator.DefaultInstance.StartJobWorkers();
                        }
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
                    _logger.LogInformation("De-initializing MassiveJobs");
                    MassiveJobsMediator.Deinitialize();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "MassiveJobs de-initialization failed");
                }
            }
        }
    }
}
