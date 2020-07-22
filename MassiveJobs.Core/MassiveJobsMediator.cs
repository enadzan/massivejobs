using System;
using System.Collections.Generic;

namespace MassiveJobs.Core
{
    public class MassiveJobsMediator : IJobPublisher, IWorkerCoordinator
    {
        protected static readonly object InitializationLock = new object();

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
                lock (InitializationLock)
                {
                    return _defaultMediator != null;
                }
            }
        }

        public static void Initialize(IJobServiceScopeFactory scopeFactory)
        {
            lock (InitializationLock)
            {
                if (IsInitializedInternal()) return;

                var scope = scopeFactory.CreateScope();

                var workerCoordinator = new WorkerCoordinator(
                    scopeFactory,
                    scope.GetRequiredService<MassiveJobsSettings>(),
                    scope.GetRequiredService<IMessageConsumer>(),
                    scope.GetService<IJobLoggerFactory>(),
                    scope.GetService<IJobLogger<WorkerCoordinator>>()
                );

                var mediator = new MassiveJobsMediator(scope.GetRequiredService<IJobPublisher>(), workerCoordinator);

                InitializeInternal(scope, workerCoordinator, mediator);
            }
        }

        protected static bool IsInitializedInternal()
        {
            return _defaultMediator != null;
        }

        protected static void InitializeInternal(IJobServiceScope scope, IWorkerCoordinator workerCoordinator, MassiveJobsMediator mediator)
        {
            _defaultScope = scope;
            _defaultWorkerCoordinator = workerCoordinator;
            _defaultMediator = mediator;
        }

        public static void Deinitialize()
        {
            lock (InitializationLock)
            {
                if (!IsInitializedInternal()) return;
                
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
