using System;
using System.Collections.Generic;

namespace MassiveJobs.Core
{
    public class MassiveJobsMediator : IJobPublisher, IWorkerCoordinator
    {
        protected static readonly object InitializationLock = new object();

        protected static MassiveJobsMediator DefaultMediator;

        public static MassiveJobsMediator DefaultInstance => DefaultMediator 
                                                             ?? throw new InvalidOperationException("MassiveJobsMediator is not initialized");

        public static bool IsInitialized
        {
            get
            {
                lock (InitializationLock)
                {
                    return DefaultMediator != null;
                }
            }
        }

        public static void Initialize(IJobServiceScopeFactory scopeFactory)
        {
            lock (InitializationLock)
            {
                if (DefaultMediator != null) return;

                DefaultMediator = new MassiveJobsMediator(scopeFactory);
            }
        }

        public static void Deinitialize()
        {
            lock (InitializationLock)
            {
                if (DefaultMediator == null) return;
                
                DefaultMediator.SafeDispose();
                DefaultMediator = null;
            }
        }
        
        protected IJobServiceScope DefaultScope;
        protected IJobServiceScopeFactory ScopeFactory;
        protected IWorkerCoordinator Workers;

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

        public virtual void Dispose()
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
