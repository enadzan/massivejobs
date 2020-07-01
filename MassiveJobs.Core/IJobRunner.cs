﻿using System.Collections.Generic;
using System.Threading;

namespace MassiveJobs.Core
{
    public interface IJobRunner
    {
        void RunJobs(IJobPublisher publisher, IReadOnlyList<JobInfo> jobs, IServiceScope serviceScope, CancellationToken cancellationToken);
    }
}