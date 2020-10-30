using System;
using System.Collections.Generic;
using System.Threading;

namespace MassiveJobs.Core
{
    public static class JobBatch
    {
        private static readonly ThreadLocal<List<JobInfo>> ActiveBatch = new ThreadLocal<List<JobInfo>>();

        internal static bool IsActive => ActiveBatch.Value != null;

        internal static void Add(JobInfo jobInfo)
        {
            if (ActiveBatch.Value == null) throw new InvalidOperationException("Batch is not active");

            ActiveBatch.Value.Add(jobInfo);
        }

        public static void Do(Action action)
        {
            try
            {
                ActiveBatch.Value = new List<JobInfo>();

                action();

                if (ActiveBatch.Value.Count > 0)
                {
                    MassiveJobsMediator.DefaultInstance.Publish(ActiveBatch.Value);
                }
            }
            finally
            {
                ActiveBatch.Value = null;
            }
        }
    }
}
