﻿using System;
using System.Collections.Generic;

namespace MassiveJobs.Core
{
    public interface IJobPublisher : IDisposable
    {
        void Publish(IEnumerable<JobInfo> jobs);

        void StartJobWorkers();
        void StopJobWorkers();
    }

    public static class JobPublisherExtensions
    {
        public static void Publish<TJob, TArgs>(this IJobPublisher publisher, TArgs jobArgs, DateTime? runAtUtc = null, int? jobTimeoutMs = null)
        {
            publisher.Publish(new[] { JobInfo.For<TJob, TArgs>(jobArgs, runAtUtc, null, jobTimeoutMs) });
        }

        public static void PublishPeriodic<TJob, TArgs>(this IJobPublisher publisher, TArgs jobArgs, string periodicJobId, int repeatSeconds, 
            DateTime? runAtUtc = null, DateTime? endAtUtc = null, int? jobTimeoutMs = null)
        {
            var periodicRunInfo = new PeriodicRunInfo
            {
                RunId = periodicJobId,
                RepeatSeconds = repeatSeconds,
                NextRunTime = runAtUtc ?? DateTime.UtcNow.AddSeconds(repeatSeconds),
                EndAtUtc = endAtUtc
            };

            publisher.Publish(new[] { JobInfo.For<TJob, TArgs>(jobArgs, periodicRunInfo.NextRunTime, null, jobTimeoutMs, periodicRunInfo) });
        }

        public static void Publish<TJob, TArgs>(this IJobPublisher publisher, TArgs jobArgs, TimeSpan runIn, int? jobTimeoutMs = null)
        {
            publisher.Publish(new[] { JobInfo.For<TJob, TArgs>(jobArgs, DateTime.UtcNow.Add(runIn), null, jobTimeoutMs) });
        }

        public static void Publish<TJob, TArgs>(this IJobPublisher publisher, IEnumerable<TArgs> jobArgs, TimeSpan runIn, int? jobTimeoutMs = null)
        {
            publisher.Publish<TJob, TArgs>(jobArgs, DateTime.UtcNow.Add(runIn), jobTimeoutMs);
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

            jobInfo.RunAtUtc = RetryTimeGenerator.GetNextRetryTime(jobInfo.Retries ?? 0);
            jobInfo.Err = ex.GetSummary();
            jobInfo.Retries = jobInfo.Retries.HasValue ? jobInfo.Retries.Value + 1 : 1;

            publisher.Publish(jobInfo);
        }
    }
}
