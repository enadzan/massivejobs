using System;

using MassiveJobs.Core.Cron;

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

        /// <summary>
        /// Cron expression
        /// </summary>
        public string CronExp { get; set; }

        /// <summary>
        /// TimeZoneInfo id
        /// </summary>
        public string TzId { get; set; }

        internal bool SetNextRunTime(DateTime? startTimeUtc, DateTime utcNow)
        {
            if (!startTimeUtc.HasValue)
            {
                startTimeUtc = utcNow;
            }

            CronSequenceGenerator cron = null;
            if (!string.IsNullOrWhiteSpace(CronExp))
            {
                cron = new CronSequenceGenerator(CronExp, TzId);
            }

            if (startTimeUtc > utcNow)
            {
                if (cron != null)
                {
                    NextRunTime = cron.NextUtc(startTimeUtc.Value.AddSeconds(-1));
                    return true;
                }

                NextRunTime = startTimeUtc.Value;
                return true;
            }

            if (cron != null)
            {
                NextRunTime = cron.NextUtc(utcNow);
                if (EndAtUtc == null || NextRunTime <= EndAtUtc.Value)
                {
                    return true;
                }
            }
            else if (RepeatSeconds > 0)
            {
                var skipWindows = ((int)utcNow.Subtract(startTimeUtc.Value).TotalSeconds) / RepeatSeconds + 1;

                NextRunTime = startTimeUtc.Value.AddSeconds(skipWindows * RepeatSeconds);
                if (EndAtUtc == null || NextRunTime <= EndAtUtc.Value)
                {
                    return true;
                }
            }

            NextRunTime = DateTime.MinValue;
            return false;
        }
    }
}
