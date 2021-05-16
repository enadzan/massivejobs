using System;
using System.Threading;

using MassiveJobs.Core;

namespace MassiveJobs.SqlServerBroker.Tests
{
    class TestTimer : ITimer
    {
        private bool _isDisposed;
        private int _dueTime = Timeout.Infinite;

        public event Action TimeElapsed;

        public void Change(int dueTime, int period)
        {
            if (_isDisposed) throw new ObjectDisposedException(nameof(TestTimer));

            _dueTime = dueTime;
        }

        public void Dispose()
        {
            _isDisposed = true;
        }

        public void Stop()
        {
            if (_isDisposed) throw new ObjectDisposedException(nameof(TestTimer));

            Change(Timeout.Infinite, Timeout.Infinite);
        }

        public void FireTimeElapsedIfActive()
        {
            if (_isDisposed || _dueTime == Timeout.Infinite) return;

            _dueTime = Timeout.Infinite;

            TimeElapsed?.Invoke();
        }
    }
}
