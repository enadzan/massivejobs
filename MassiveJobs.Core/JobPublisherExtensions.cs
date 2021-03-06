﻿using System;
using System.Collections.Generic;

namespace MassiveJobs.Core
{
    public static class JobPublisherExtensions
    {
        public static void Publish<TJob, TArgs>(this IJobPublisher publisher, TArgs jobArgs, DateTime? runAtUtc = null, int? jobTimeoutMs = null)
        {
            publisher.Publish(new[] { JobInfo.For<TJob, TArgs>(jobArgs, runAtUtc, null, jobTimeoutMs) });
        }

        public static void PublishPeriodic<TJob, TArgs>(this IJobPublisher publisher, TArgs jobArgs, string groupKey, int repeatSeconds, 
            DateTime? runAtUtc = null, DateTime? endAtUtc = null, int? jobTimeoutMs = null)
        {
            var periodicRunInfo = new PeriodicRunInfo
            {
                RepeatSeconds = repeatSeconds,
                NextRunTime = runAtUtc ?? DateTime.UtcNow.AddSeconds(repeatSeconds),
                EndAtUtc = endAtUtc
            };

            publisher.Publish(new[] { JobInfo.For<TJob, TArgs>(jobArgs, periodicRunInfo.NextRunTime, groupKey, jobTimeoutMs, periodicRunInfo) });
        }

        public static void PublishPeriodic<TJob, TArgs>(this IJobPublisher publisher, TArgs jobArgs, string groupKey, string cronExpression, 
            TimeZoneInfo timeZoneInfo = null, DateTime? runAtUtc = null, DateTime? endAtUtc = null, int? jobTimeoutMs = null)
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

            publisher.Publish(new[] { JobInfo.For<TJob, TArgs>(jobArgs, periodicRunInfo.NextRunTime, groupKey, jobTimeoutMs, periodicRunInfo) });
        }

        public static void CancelPeriodic<TJob, TArgs>(this IJobPublisher publisher, TArgs jobArgs, string groupKey)
        {
            var periodicRunInfo = new PeriodicRunInfo
            {
                NextRunTime = DateTime.MinValue,
                LastRunTimeUtc = DateTime.MinValue, //important to replace existing job
            };

            publisher.Publish(new[] { JobInfo.For<TJob, TArgs>(jobArgs, periodicRunInfo.NextRunTime, groupKey, null, periodicRunInfo) });
        }

        public static void Publish<TJob, TArgs>(this IJobPublisher publisher, TArgs jobArgs, TimeSpan runIn, int? jobTimeoutMs = null)
        {
            publisher.Publish(new[] { JobInfo.For<TJob, TArgs>(jobArgs, DateTime.UtcNow.Add(runIn), null, jobTimeoutMs) });
        }

        public static void Publish<TJob, TArgs>(this IJobPublisher publisher, IEnumerable<TArgs> jobArgs, TimeSpan runIn, int? jobTimeoutMs = null)
        {
            publisher.Publish<TJob, TArgs>(jobArgs, DateTime.UtcNow.Add(runIn), jobTimeoutMs);
        }

        public static void Publish<TJob>(this IJobPublisher publisher, DateTime? runAtUtc = null, int? jobTimeoutMs = null)
        {
            publisher.Publish(new[] { JobInfo.For<TJob, VoidArgs>(null, runAtUtc, null, jobTimeoutMs) });
        }

        public static void PublishPeriodic<TJob>(this IJobPublisher publisher, string groupKey, int repeatSeconds, 
            DateTime? runAtUtc = null, DateTime? endAtUtc = null, int? jobTimeoutMs = null)
        {
            PublishPeriodic<TJob, VoidArgs>(publisher, null, groupKey, repeatSeconds, runAtUtc, endAtUtc, jobTimeoutMs);
        }

        public static void PublishPeriodic<TJob>(this IJobPublisher publisher, string groupKey, string cronExpression, 
            TimeZoneInfo timeZoneInfo = null, DateTime? runAtUtc = null, DateTime? endAtUtc = null, int? jobTimeoutMs = null)
        {
            PublishPeriodic<TJob, VoidArgs>(publisher, null, groupKey, cronExpression, timeZoneInfo, runAtUtc, endAtUtc, jobTimeoutMs);
        }

        public static void CancelPeriodic<TJob>(this IJobPublisher publisher, string groupKey)
        {
            CancelPeriodic<TJob, VoidArgs>(publisher, null, groupKey);
        }

        public static void Publish<TJob>(this IJobPublisher publisher, TimeSpan runIn, int? jobTimeoutMs = null)
        {
            publisher.Publish(new[] { JobInfo.For<TJob, VoidArgs>(null, DateTime.UtcNow.Add(runIn), null, jobTimeoutMs) });
        }

        public static void Publish<TJob, TArgs>(this IJobPublisher publisher, IEnumerable<TArgs> jobArgs, DateTime? runAtUtc = null, int? jobTimeoutMs = null, 
            PeriodicRunInfo periodicRunInfo = null)
        {
            var jobs = new List<JobInfo>();

            foreach (var jobArg in jobArgs)
            {
                jobs.Add(JobInfo.For<TJob, TArgs>(jobArg, runAtUtc, null, jobTimeoutMs, periodicRunInfo));
            }

            publisher.Publish(jobs);
        }

        public static void Publish(this IJobPublisher publisher, JobInfo jobInfo)
        {
            publisher.Publish(new[] { jobInfo });
        }

        public static void RescheduleJob(this IJobPublisher publisher, JobInfo jobInfo, Exception ex)
        {
            if (jobInfo.PeriodicRunInfo != null) return; // periodic jobs must not be rescheduled

            jobInfo.HasErrors = true;
            jobInfo.RunAtUtc = RetryTimeGenerator.GetNextRetryTime(jobInfo.Retries ?? 0);
            jobInfo.Err = ex.GetSummary();
            jobInfo.Retries = jobInfo.Retries.HasValue ? jobInfo.Retries.Value + 1 : 1;

            publisher.Publish(jobInfo);
        }
    }
}
