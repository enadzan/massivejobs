using System;
using System.Collections.Generic;
using MassiveJobs.Core.DependencyInjection;

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

        public static void Initialize(IJobServiceFactory serviceFactory)
        {
            lock (InitializationLock)
            {
                if (DefaultMediator != null) return;
                DefaultMediator = new MassiveJobsMediator(serviceFactory);
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
        
        protected IJobServiceFactory ServiceFactory;
        protected IWorkerCoordinator Workers;

        protected MassiveJobsMediator()
        {
        }

        public MassiveJobsMediator(IJobServiceFactory serviceFactory)
        {
            ServiceFactory = serviceFactory;
            Workers = new WorkerCoordinator(serviceFactory);
        }

        public virtual void Dispose()
        {
            Workers.SafeDispose();

            if (ServiceFactory is IDisposable disposable)
            {
                disposable.SafeDispose();
            }
        }

        public void Publish(IEnumerable<JobInfo> jobs)
        {
            using (var scope = ServiceFactory.GetRequiredService<IJobServiceScopeFactory>().CreateScope())
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
