using System;

namespace MassiveJobs.Core.Tests
{
    class TestTimeProvider : ITimeProvider
    {
        public DateTime CurrentTimeUtc { get; set; }
        public DateTime GetCurrentTimeUtc()
        {
            return CurrentTimeUtc;
        }

        internal void AdvanceTime(int ms)
        {
            CurrentTimeUtc = CurrentTimeUtc.AddMilliseconds(ms);
        }
    }
}
