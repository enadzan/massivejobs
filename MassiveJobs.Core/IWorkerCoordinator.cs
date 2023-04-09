using System;

namespace MassiveJobs.Core
{
    public interface IWorkerCoordinator: IDisposable
    {
        void StartJobWorkers();
        void StopJobWorkers();
    }
}