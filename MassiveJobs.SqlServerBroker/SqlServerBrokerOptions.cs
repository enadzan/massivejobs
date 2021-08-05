namespace MassiveJobs.SqlServerBroker
{
    public class SqlServerBrokerOptions
    {
        /// <summary>
        /// If set to false (default = true), the processed messages will not be deleted from the message queue.
        /// It is expected that the DBA will set up a job to perform periodic deletion of old processed messages.
        /// </summary>
        public bool DeleteProcessedMessages { get; set; } = true;
    }
}
