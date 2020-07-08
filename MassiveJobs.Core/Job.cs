using System;
using System.Threading;

namespace MassiveJobs.Core
{
    public abstract class Job<TJob, TArgs>
    {
        public abstract void Perform(TArgs args, CancellationToken cancellationToken);

        public static void PerformAsync(TArgs args, int? timeoutMs = null)
        {
            if (JobBatch.IsActive)
            {
                JobBatch.Add(JobInfo.For<TJob, TArgs>(args, null, timeoutMs));
                return;
            }

            MassiveJobsMediator.DefaultInstance.Publish<TJob, TArgs>(args, null, timeoutMs);
        }

        public static void PerformAsync(TArgs args, TimeSpan runIn, int? timeoutMs)
        {
            if (JobBatch.IsActive)
            {
                JobBatch.Add(JobInfo.For<TJob, TArgs>(args, runIn, null, timeoutMs));
                return;
            }

            MassiveJobsMediator.DefaultInstance.Publish<TJob, TArgs>(args, runIn, timeoutMs);
        }

        public static void PerformAsync(TArgs args, DateTime runAtUtc, int? timeoutMs)
        {
            if (JobBatch.IsActive)
            {
                JobBatch.Add(JobInfo.For<TJob, TArgs>(args, runAtUtc, null, timeoutMs));
                return;
            }

            MassiveJobsMediator.DefaultInstance.Publish<TJob, TArgs>(args, runAtUtc, timeoutMs);
        }

        public static void PerformPeriodic(TArgs args, string groupKey, int repeatSec, 
            DateTime? runAtUtc = null, DateTime? endAtUtc = null, int? timeoutMs = null)
        {
            if (JobBatch.IsActive)
            {
                var periodicRunInfo = new PeriodicRunInfo
                {
                    RepeatSeconds = repeatSec,
                    NextRunTime = runAtUtc ?? DateTime.UtcNow.AddSeconds(repeatSec),
                    EndAtUtc = endAtUtc
                };

                JobBatch.Add(JobInfo.For<TJob, TArgs>(args, periodicRunInfo.NextRunTime, groupKey, timeoutMs, periodicRunInfo));
                return;
            }

            MassiveJobsMediator.DefaultInstance.PublishPeriodic<TJob, TArgs>(args, groupKey, repeatSec, runAtUtc, endAtUtc, timeoutMs);
        }

        public static void PerformPeriodic(TArgs args, string groupKey, string cronExpression,
            TimeZoneInfo timeZoneInfo = null, DateTime? runAtUtc = null, DateTime? endAtUtc = null, int? timeoutMs = null)
        {
            if (JobBatch.IsActive)
            {
                if (!Cron.CronSequenceGenerator.IsValidExpression(cronExpression))
                {
                    throw new ArgumentException($"Invalid cron expression ('{cronExpression}')", nameof(cronExpression));
                }

                var periodicRunInfo = new PeriodicRunInfo
                {
                    NextRunTime = runAtUtc ?? DateTime.UtcNow,
                    EndAtUtc = endAtUtc,
                    CronExp = cronExpression,
                    TzId = timeZoneInfo?.Id
                };

                JobBatch.Add(JobInfo.For<TJob, TArgs>(args, periodicRunInfo.NextRunTime, groupKey, timeoutMs, periodicRunInfo));
                return;
            }

            MassiveJobsMediator.DefaultInstance.PublishPeriodic<TJob, TArgs>(args, groupKey, cronExpression, timeZoneInfo, runAtUtc, endAtUtc, timeoutMs);
        }
    }

    public abstract class Job<TJob>
    {
        public abstract void Perform(CancellationToken cancellationToken);

        public static void PerformAsync(int? timeoutMs = null)
        {
            Job<TJob, VoidArgs>.PerformAsync(null, timeoutMs);
        }

        public static void PerformAsync(TimeSpan runIn, int? timeoutMs)
        {
            Job<TJob, VoidArgs>.PerformAsync(null, runIn, timeoutMs);
        }

        public static void PerformAsync(DateTime runAtUtc, int? timeoutMs)
        {
            Job<TJob, VoidArgs>.PerformAsync(null, runAtUtc, timeoutMs);
        }

        public static void PerformPeriodic(string groupKey, int repeatSec, 
            DateTime? runAtUtc = null, DateTime? endAtUtc = null, int? timeoutMs = null)
        {
            Job<TJob, VoidArgs>.PerformPeriodic(null, groupKey, repeatSec, runAtUtc, endAtUtc, timeoutMs);
        }

        public static void PerformPeriodic(string groupKey, string cronExpression, 
            TimeZoneInfo timeZoneInfo = null, DateTime? runAtUtc = null, DateTime? endAtUtc = null, int? timeoutMs = null)
        {
            Job<TJob, VoidArgs>.PerformPeriodic(null, groupKey, cronExpression, timeZoneInfo, runAtUtc, endAtUtc, timeoutMs);
        }
    }


}
