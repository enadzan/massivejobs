using System;

namespace MassiveJobs.Core
{
    public interface ITimer: IDisposable
    {
        event Action TimeElapsed;

        void Change(int dueTime, int period);
        void Stop();
    }
}
