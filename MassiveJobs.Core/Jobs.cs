using System.Collections.Generic;

namespace MassiveJobs.Core
{
    public class Jobs : IJobPublisher, IWorkerCoordinator
    {
        private readonly ILogger<Jobs> _logger;

        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IServiceScope _scope;

        private readonly IJobPublisher _publisher;
        private readonly IWorkerCoordinator _workers;

        public Jobs(IServiceScopeFactory scopeFactory, ILogger<Jobs> logger)
            : this(scopeFactory.CreateScope(), logger)
        {
            _scopeFactory = scopeFactory;
        }

        public Jobs(IServiceScope scope, ILogger<Jobs> logger)
            : this(scope.GetService<IJobPublisher>(), scope.GetService<IWorkerCoordinator>(), logger)
        {
            _scope = scope;
        }

        public Jobs(IJobPublisher publisher, IWorkerCoordinator workers, ILogger<Jobs> logger)
        {
            _logger = logger;
            _publisher = publisher;
            _workers = workers;
        }

        public void Dispose()
        {
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
