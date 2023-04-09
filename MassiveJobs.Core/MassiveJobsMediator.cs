using Microsoft.Extensions.DependencyInjection;
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

        public static void Initialize(IServiceProvider serviceProvider)
        {
            lock (InitializationLock)
            {
                if (DefaultMediator != null) return;
                DefaultMediator = new MassiveJobsMediator(serviceProvider);
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
        
        protected IServiceProvider ServiceProvider;
        protected IWorkerCoordinator Workers;

        protected MassiveJobsMediator()
        {
        }

        public MassiveJobsMediator(IServiceProvider serviceProvider)
        {
            ServiceProvider = serviceProvider;
            Workers = ServiceProvider.GetRequiredService<IWorkerCoordinator>();
        }

        public virtual void Dispose()
        {
            Workers.SafeDispose();
        }

        public void Publish(IEnumerable<JobInfo> jobs)
        {
            using (var scope = ServiceProvider.CreateScope())
            {
                scope.ServiceProvider.GetRequiredService<IJobPublisher>().Publish(jobs);
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
