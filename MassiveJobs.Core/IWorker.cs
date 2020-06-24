using System;

namespace MassiveJobs.Core
{
    public interface IWorker: IDisposable
    {
        event Action<Exception> Error;

        void Start();
        void BeginStop();
        void WaitUntilStopped();
    }
}