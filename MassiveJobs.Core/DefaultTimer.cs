using System;
using System.Threading;

namespace MassiveJobs.Core
{
    public class DefaultTimer : ITimer
    {
        private readonly Timer _timer;

        public event Action TimeElapsed;

        public DefaultTimer()
        {
            _timer = new Timer(TimerCallback);
        }

        public void Dispose()
        {
            _timer.Dispose();
        }

        public void Change(int dueTime, int period)
        {
            _timer.Change(dueTime, period);
        }

        public void Stop()
        {
            Change(Timeout.Infinite, Timeout.Infinite);
        }

        public void TimerCallback(object state)
        {
            TimeElapsed?.Invoke();
        }
    }
}
