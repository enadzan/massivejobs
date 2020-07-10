using System.Collections.Generic;

namespace MassiveJobs.Core
{
    public class MassiveJobsMediator : IJobPublisher, IWorkerCoordinator
    {
        private static readonly object _initializationLock = new object();

        public static MassiveJobsMediator DefaultInstance { get; private set; }

        private static IJobServiceScope _defaultScope;
        private static IWorkerCoordinator _defaultWorkerCoordinator;

        public static void Initialize(IJobServiceScopeFactory scopeFactory)
        {
            lock (_initializationLock)
            {
                if (DefaultInstance != null) return;

                _defaultScope = scopeFactory.CreateScope();

                _defaultWorkerCoordinator = new WorkerCoordinator(
                    scopeFactory,
                    _defaultScope.GetService<MassiveJobsSettings>(),
                    _defaultScope.GetService<IMessageConsumer>(),
                    _defaultScope.GetService<IJobLoggerFactory>(),
                    _defaultScope.GetService<IJobLogger<WorkerCoordinator>>()
                );

                DefaultInstance = new MassiveJobsMediator(_defaultScope.GetService<IJobPublisher>(), _defaultWorkerCoordinator);
            }
        }

        public static void Deinitialize()
        {
            lock (_initializationLock)
            {
                if (DefaultInstance == null) return;
                
                _defaultWorkerCoordinator.SafeDispose();
                _defaultScope.SafeDispose();

                _defaultWorkerCoordinator = null;
                _defaultScope = null;

                DefaultInstance = null;
            }
        }
        
        protected readonly IJobPublisher Publisher;
        protected readonly IWorkerCoordinator Workers;

        public MassiveJobsMediator(IJobPublisher jobPublisher, IWorkerCoordinator workerCoordinator)
        {
            Publisher = jobPublisher;
            Workers = workerCoordinator;
        }

        public void Dispose()
        {
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
