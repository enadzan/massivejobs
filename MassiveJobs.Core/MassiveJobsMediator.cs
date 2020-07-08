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
        

        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IServiceScope _scope;

        protected readonly ILogger<MassiveJobsMediator> Logger;
        protected readonly IJobPublisher Publisher;
        protected readonly IWorkerCoordinator Workers;

        public MassiveJobsMediator(IServiceScopeFactory scopeFactory, ILogger<MassiveJobsMediator> logger)
        {
            _scopeFactory = scopeFactory;
            _scope = scopeFactory.CreateScope();

            Logger = logger;
            Publisher = _scope.GetService<IJobPublisher>();
            Workers = _scope.GetService<IWorkerCoordinator>();
        }

        public void Dispose()
        {
            Workers.StopJobWorkers();

            _scope.SafeDispose(Logger);
            _scopeFactory.SafeDispose(Logger);
        }

        public void Publish(IEnumerable<JobInfo> jobs)
        {
            Publisher.Publish(jobs);
        }

        public void StartJobWorkers()
        {
            Workers.StartJobWorkers();
        }

        public void StopJobWorkers()
        {
            Workers.StopJobWorkers();
        }
    }
}
