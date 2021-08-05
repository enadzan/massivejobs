using System;
using System.Threading;
using System.Threading.Tasks;

namespace MassiveJobs.Core
{
    public abstract class JobAsync<TJob, TArgs>
    {
        public abstract Task Perform(TArgs args, CancellationToken cancellationToken);

        public static void Publish(TArgs args, int? timeoutMs = null)
        {
            Publish(args, MassiveJobsMediator.DefaultInstance, timeoutMs);
        }

        public static void Publish(TArgs args, IJobPublisher publisher, int? timeoutMs = null)
        {
            if (JobBatch.IsActive)
            {
                JobBatch.Add(JobInfo.For<TJob, TArgs>(args, null, timeoutMs));
                return;
            }

            publisher.Publish<TJob, TArgs>(args, null, timeoutMs);
        }

        public static void Publish(TArgs args, TimeSpan runIn, int? timeoutMs = null)
        {
            Publish(args, runIn, MassiveJobsMediator.DefaultInstance, timeoutMs);
        }

        public static void Publish(TArgs args, TimeSpan runIn, IJobPublisher publisher, int? timeoutMs = null)
        {
            if (JobBatch.IsActive)
            {
                JobBatch.Add(JobInfo.For<TJob, TArgs>(args, runIn, null, timeoutMs));
                return;
            }

            publisher.Publish<TJob, TArgs>(args, runIn, timeoutMs);
        }

        public static void Publish(TArgs args, DateTime runAtUtc, int? timeoutMs = null)
        {
            Publish(args, runAtUtc, MassiveJobsMediator.DefaultInstance, timeoutMs);
        }

        public static void Publish(TArgs args, DateTime runAtUtc, IJobPublisher publisher, int? timeoutMs = null)
        {
            if (JobBatch.IsActive)
            {
                JobBatch.Add(JobInfo.For<TJob, TArgs>(args, runAtUtc, null, timeoutMs));
                return;
            }

            publisher.Publish<TJob, TArgs>(args, runAtUtc, timeoutMs);
        }

        public static void CancelPeriodic(string groupKey)
        {
            CancelPeriodic(groupKey, MassiveJobsMediator.DefaultInstance);
        }

        public static void CancelPeriodic(string groupKey, IJobPublisher publisher)
        {
            if (JobBatch.IsActive)
            {
                var periodicRunInfo = new PeriodicRunInfo
                {
                    NextRunTime = DateTime.MinValue,
                    LastRunTimeUtc = DateTime.MinValue, //important to replace existing job
                };

                JobBatch.Add(JobInfo.For<TJob, VoidArgs>(null, periodicRunInfo.NextRunTime, groupKey, null, periodicRunInfo));
                return;
            }

            publisher.CancelPeriodic<TJob>(groupKey);
        }

        public static void PublishPeriodic(TArgs args, string groupKey, int repeatSec,
            DateTime? runAtUtc = null, DateTime? endAtUtc = null, int? timeoutMs = null)
        {
            PublishPeriodic(args, groupKey, repeatSec, MassiveJobsMediator.DefaultInstance, runAtUtc, endAtUtc, timeoutMs);
        }

        public static void PublishPeriodic(TArgs args, string groupKey, int repeatSec, IJobPublisher publisher, 
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

            publisher.PublishPeriodic<TJob, TArgs>(args, groupKey, repeatSec, runAtUtc, endAtUtc, timeoutMs);
        }

        public static void PublishPeriodic(TArgs args, string groupKey, string cronExpression,
            TimeZoneInfo timeZoneInfo = null, DateTime? runAtUtc = null, DateTime? endAtUtc = null, int? timeoutMs = null)
        {
            PublishPeriodic(args, groupKey, cronExpression, MassiveJobsMediator.DefaultInstance, timeZoneInfo, runAtUtc, endAtUtc, timeoutMs);
        }

        public static void PublishPeriodic(TArgs args, string groupKey, string cronExpression, IJobPublisher publisher,
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

            publisher.PublishPeriodic<TJob, TArgs>(args, groupKey, cronExpression, timeZoneInfo, runAtUtc, endAtUtc, timeoutMs);
        }

        protected static void Publish(TArgs args, string groupKey, int? timeoutMs = null)
        {
            Publish(args, groupKey, MassiveJobsMediator.DefaultInstance, timeoutMs);
        }

        protected static void Publish(TArgs args, string groupKey, IJobPublisher publisher, int? timeoutMs = null)
        {
            var jobInfo = JobInfo.For<TJob, TArgs>(args, groupKey, timeoutMs);

            if (JobBatch.IsActive)
            {
                JobBatch.Add(jobInfo);
                return;
            }

            publisher.Publish(jobInfo);
        }
    }

    public abstract class JobAsync<TJob>
    {
        public abstract Task Perform(CancellationToken cancellationToken);

        public static void Publish(int? timeoutMs = null)
        {
            JobAsync<TJob, VoidArgs>.Publish(null, timeoutMs);
        }

        public static void Publish(TimeSpan runIn, int? timeoutMs)
        {
            JobAsync<TJob, VoidArgs>.Publish(null, runIn, timeoutMs);
        }

        public static void Publish(DateTime runAtUtc, int? timeoutMs)
        {
            JobAsync<TJob, VoidArgs>.Publish(null, runAtUtc, timeoutMs);
        }

        public static void CancelPeriodic(string groupKey)
        {
            JobAsync<TJob, VoidArgs>.CancelPeriodic(groupKey);
        }

        public static void PublishPeriodic(string groupKey, int repeatSec, 
            DateTime? runAtUtc = null, DateTime? endAtUtc = null, int? timeoutMs = null)
        {
            JobAsync<TJob, VoidArgs>.PublishPeriodic(null, groupKey, repeatSec, runAtUtc, endAtUtc, timeoutMs);
        }

        public static void PublishPeriodic(string groupKey, string cronExpression, 
            TimeZoneInfo timeZoneInfo = null, DateTime? runAtUtc = null, DateTime? endAtUtc = null, int? timeoutMs = null)
        {
            JobAsync<TJob, VoidArgs>.PublishPeriodic(null, groupKey, cronExpression, timeZoneInfo, runAtUtc, endAtUtc, timeoutMs);
        }
    }

    public abstract class Job<TJob, TArgs>: JobAsync<TJob, TArgs>
    {
        public abstract void Perform(TArgs args);

        public override Task Perform(TArgs args, CancellationToken cancellationToken)
        {
            Perform(args);
            return Task.CompletedTask;
        }
    }

    public abstract class Job<TJob>: JobAsync<TJob>
    {
        public abstract void Perform();

        public override Task Perform(CancellationToken cancellationToken)
        {
            Perform();
            return Task.CompletedTask;
        }
    }


}
