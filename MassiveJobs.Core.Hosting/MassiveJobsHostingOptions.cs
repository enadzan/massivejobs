using System;

namespace MassiveJobs.Core.Hosting
{
    public class MassiveJobsHostingOptions
    {
        public MassiveJobsSettings MassiveJobsSettings { get; set; }

        public bool StartWorkers { get; set; } = true;
        public Action OnInitAction { get; set; }
    }
}
