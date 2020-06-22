using System;

namespace MassiveJobs.Core
{
    public class JobInfo
    {
        public Type JobType { get; set; }
        public Type ArgsType { get; set; }
        public object Args { get; set; }
        public DateTime? RunAtUtc { get; set; }

        public int? Retries { get; set; }
        public string Err { get; set; }
        public int? TimeoutMs { get; set; }

        public string GroupKey { get; set; }

        public PeriodicRunInfo PeriodicRunInfo { get; set; }

        public static JobInfo For<TJob, TJobArgs>(TJobArgs jobArgs, TimeSpan runIn, string groupKey = null, int? jobTimeoutMs = null, PeriodicRunInfo periodicRunInfo = null)
        {
            return For<TJob, TJobArgs>(jobArgs, DateTime.UtcNow.Add(runIn), groupKey, jobTimeoutMs, periodicRunInfo);
        }

        public static JobInfo For<TJob, TJobArgs>(TJobArgs jobArgs, string groupKey = null, int? jobTimeoutMs = null, PeriodicRunInfo periodicRunInfo = null)
        {
            return For<TJob, TJobArgs>(jobArgs, null, groupKey, jobTimeoutMs, periodicRunInfo);
        }

        public static JobInfo For<TJob, TJobArgs>(TJobArgs jobArgs, DateTime? runAtUtc, string groupKey = null, int? jobTimeoutMs = null, PeriodicRunInfo periodicRunInfo = null)
        {
            return new JobInfo
            {
                RunAtUtc = runAtUtc,
                JobType = typeof(TJob),
                ArgsType = typeof(TJobArgs),
                Args = jobArgs,
                TimeoutMs = jobTimeoutMs,
                GroupKey = groupKey,
                PeriodicRunInfo = periodicRunInfo
            };
        }
    }
}
