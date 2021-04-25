using System;

namespace MassiveJobs.SqlServerBroker
{
    public class MessageQueue
    {
        public string RoutingKey { get; set; }
        public long Id { get; set; }
        public string MessageType { get; set; }
        public string MessageData { get; set; }
        public DateTime PublishedAtUtc { get; set; }
        public string ProcessingInstance { get; set; }
        public DateTime? ProcessingStartUtc { get; set; }
        public DateTime? ProcessingKeepAliveUtc { get; set; }
        public DateTime? ProcessingEndUtc { get; set; }
    }
}
