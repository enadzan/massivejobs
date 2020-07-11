using System;
using System.Collections.Generic;

namespace MassiveJobs.Core
{
    public class MassiveJobsMediator : IJobPublisher, IWorkerCoordinator
    {
        private static readonly object _initializationLock = new object();

        private static MassiveJobsMediator _defaultMediator;
        public static MassiveJobsMediator DefaultInstance 
        { 
            get
            {
                return _defaultMediator ?? throw new InvalidOperationException("MassiveJobsMediator is not initialized");
            }
        }

        private static IJobServiceScope _defaultScope;
        private static IWorkerCoordinator _defaultWorkerCoordinator;

        public static bool IsInitialized
        {
            get
            {
                lock (_initializationLock)
                {
                    return _defaultMediator != null;
                }
            }
        }

        public static void Initialize(IJobServiceScopeFactory scopeFactory)
        {
            lock (_initializationLock)
            {
                if (_defaultMediator != null) return;

                _defaultScope = scopeFactory.CreateScope();

                _defaultWorkerCoordinator = new WorkerCoordinator(
                    scopeFactory,
                    _defaultScope.GetRequiredService<MassiveJobsSettings>(),
                    _defaultScope.GetRequiredService<IMessageConsumer>(),
                    _defaultScope.GetService<IJobLoggerFactory>(),
                    _defaultScope.GetService<IJobLogger<WorkerCoordinator>>()
                );

                _defaultMediator = new MassiveJobsMediator(_defaultScope.GetRequiredService<IJobPublisher>(), _defaultWorkerCoordinator);
            }
        }

        public static void Deinitialize()
        {
            lock (_initializationLock)
            {
                if (_defaultMediator == null) return;
                
                _defaultWorkerCoordinator.SafeDispose();
                _defaultScope.SafeDispose();

                _defaultWorkerCoordinator = null;
                _defaultScope = null;

                _defaultMediator = null;
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
