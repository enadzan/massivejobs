using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using MassiveJobs.Core.DependencyInjection;

namespace MassiveJobs.Core.Hosting
{
    public class MassiveJobsBackgroundService : BackgroundService
    {
        private readonly ILogger<MassiveJobsBackgroundService> _logger;

        private readonly IJobServiceFactory _jobServiceFactory;
        private readonly MassiveJobsHostingOptions _options;

        public MassiveJobsBackgroundService(IJobServiceFactory jobServiceFactory, MassiveJobsHostingOptions options,
            ILogger<MassiveJobsBackgroundService> logger)
        {
            _jobServiceFactory = jobServiceFactory;
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

                        MassiveJobsMediator.Initialize(_jobServiceFactory);

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
