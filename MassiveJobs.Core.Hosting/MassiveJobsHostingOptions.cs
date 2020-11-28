using System;

namespace MassiveJobs.Core.Hosting
{
    public class MassiveJobsHostingOptions
    {
        public MassiveJobsSettings MassiveJobsSettings { get; } = new MassiveJobsSettings();

        public bool StartWorkers { get; set; }
        public Action OnInitAction { get; set; }
    }
}
