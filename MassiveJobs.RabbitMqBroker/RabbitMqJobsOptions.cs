
using MassiveJobs.Core;

namespace MassiveJobs.RabbitMqBroker
{
    public class RabbitMqJobsOptions
    {
        public MassiveJobsSettings MassiveJobs { get; set; }
        public IJobSerializer JobSerializer { get; set; }
        public IJobTypeProvider JobTypeProvider { get; set; }
        public IJobLoggerFactory JobLoggerFactory { get; set; }
    }
}
