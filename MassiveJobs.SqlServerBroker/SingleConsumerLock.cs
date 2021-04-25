using System;

namespace MassiveJobs.SqlServerBroker
{
    public class SingleConsumerLock
    {
        public string RoutingKey { get; set; }
        public string InstanceName { get; set; }
        public DateTime? LockKeepAliveUtc { get; set; }
    }
}
