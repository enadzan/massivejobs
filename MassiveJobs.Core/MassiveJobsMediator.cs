using System.Collections.Generic;

namespace MassiveJobs.Core
{
    public class MassiveJobsMediator : IJobPublisher, IWorkerCoordinator
    {
        public static MassiveJobsMediator DefaultInstance { get; private set; }

        public static void Initialize(MassiveJobsMediator jobs, bool startWorkers = true)
        {
            DefaultInstance = jobs;

            if (startWorkers)
            {
                DefaultInstance.StartJobWorkers();
            }
        }
        
        private readonly ILogger<MassiveJobsMediator> _logger;

        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IServiceScope _scope;

        private readonly IJobPublisher _publisher;
        private readonly IWorkerCoordinator _workers;

        public MassiveJobsMediator(IServiceScopeFactory scopeFactory, ILogger<MassiveJobsMediator> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;

            _scope = scopeFactory.CreateScope();

            _publisher = _scope.GetService<IJobPublisher>();
            _workers = _scope.GetService<IWorkerCoordinator>();
        }

        public void Dispose()
        {
            _workers.StopJobWorkers();

            _scope.SafeDispose(_logger);
            _scopeFactory.SafeDispose(_logger);
        }

        public void Publish(IEnumerable<JobInfo> jobs)
        {
            _publisher.Publish(jobs);
        }

        public void StartJobWorkers()
        {
            _workers.StartJobWorkers();
        }

        public void StopJobWorkers()
        {
            _workers.StopJobWorkers();
        }
    }
}
