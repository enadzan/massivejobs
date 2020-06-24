using System;

namespace MassiveJobs.Core
{
    public class PeriodicRunInfo
    {
        /// <summary>
        /// Repeat the job every specified number of seconds. 
        /// </summary>
        public int RepeatSeconds { get; set; }

        public DateTime? EndAtUtc { get; set; }
        public DateTime? LastRunTimeUtc { get; set; }
        public DateTime NextRunTime { get; set; }

        internal bool SetNextRunTime(DateTime? startTimeUtc, DateTime utcNow)
        {
            if (!startTimeUtc.HasValue)
            {
                startTimeUtc = utcNow;
            }

            if (startTimeUtc > utcNow)
            {
                NextRunTime = startTimeUtc.Value;
                return true;
            }

            if (RepeatSeconds > 0)
            {
                var skipWindows = ((int)utcNow.Subtract(startTimeUtc.Value).TotalSeconds) / RepeatSeconds + 1;

                NextRunTime = startTimeUtc.Value.AddSeconds(skipWindows * RepeatSeconds);

                if (EndAtUtc == null || EndAtUtc.Value > NextRunTime) return true;
            }

            NextRunTime = DateTime.MinValue;
            return false;
        }
    }
}
