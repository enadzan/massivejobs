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

        public int GetGroupKeyHashCode()
        {
            int hash1 = (5381 << 16) + 5381;
            int hash2 = hash1;

            for (int i = 0; i < GroupKey.Length; i++)
            {
                int c = GroupKey[i] + 1;
                hash1 = unchecked((hash1 << 5) + hash1) ^ c;

                if (++i >= GroupKey.Length)
                    break;

                c = GroupKey[i] + 1;
                hash2 = unchecked((hash2 << 5) + hash2) ^ c;
            }

            return unchecked(hash1 + (hash2 * 1566083941));
        }

        public JobInfo ToImmediateJob()
        {
            return new JobInfo
            {
                JobType = JobType,
                ArgsType = ArgsType,
                Args = Args,
                Retries = Retries,
                Err = Err,
                TimeoutMs = TimeoutMs,
                RunAtUtc = null,
                GroupKey = null,
                PeriodicRunInfo = null
            };
        }

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
