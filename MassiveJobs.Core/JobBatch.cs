using System;
using System.Collections.Generic;
using System.Threading;

namespace MassiveJobs.Core
{
    public static class JobBatch
    {
        private static readonly ThreadLocal<List<JobInfo>> _activeBatch = new ThreadLocal<List<JobInfo>>();

        internal static bool IsActive => _activeBatch.Value != null;

        internal static void Add(JobInfo jobInfo)
        {
            if (_activeBatch.Value == null) throw new InvalidOperationException("Batch is not active");

            _activeBatch.Value.Add(jobInfo);
        }

        public static void Do(Action action)
        {
            try
            {
                _activeBatch.Value = new List<JobInfo>();

                action();

                if (_activeBatch.Value.Count > 0)
                {
                    MassiveJobsMediator.DefaultInstance.Publish(_activeBatch.Value);
                }
            }
            finally
            {
                _activeBatch.Value = null;
            }
        }
    }
}
