using System;
using System.Collections.Generic;

namespace MassiveJobs.Core
{
    public class MassiveJobsMediator : IJobPublisher, IWorkerCoordinator
    {
        protected static readonly object InitializationLock = new object();

        private static MassiveJobsMediator _defaultMediator;

        public static MassiveJobsMediator DefaultInstance => _defaultMediator 
                                                             ?? throw new InvalidOperationException("MassiveJobsMediator is not initialized");

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
                if (_defaultMediator != null) return;

                _defaultMediator = new MassiveJobsMediator(scopeFactory);
            }
        }

        public static void Deinitialize()
        {
            lock (InitializationLock)
            {
                if (_defaultMediator == null) return;
                
                _defaultMediator.SafeDispose();
                _defaultMediator = null;
            }
        }
        
        protected readonly IJobServiceScope DefaultScope;
        protected readonly IJobServiceScopeFactory ScopeFactory;
        protected readonly IWorkerCoordinator Workers;

        protected MassiveJobsMediator()
        {
        }

        public MassiveJobsMediator(IJobServiceScopeFactory scopeFactory)
        {
            ScopeFactory = scopeFactory;

            DefaultScope = scopeFactory.CreateScope();

            Workers = new WorkerCoordinator(
                scopeFactory,
                DefaultScope.GetRequiredService<MassiveJobsSettings>(),
                DefaultScope.GetRequiredService<IMessageConsumer>(),
                DefaultScope.GetService<IJobLoggerFactory>(),
                DefaultScope.GetService<IJobLogger<WorkerCoordinator>>()
            );
        }

        public void Dispose()
        {
            Workers.SafeDispose();
            DefaultScope.SafeDispose(); 
        }

        public void Publish(IEnumerable<JobInfo> jobs)
        {
            using (var scope = ScopeFactory.CreateScope())
            {
                scope.GetRequiredService<IJobPublisher>().Publish(jobs);
            }
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
