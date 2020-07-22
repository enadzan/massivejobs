using System;

namespace MassiveJobs.Core
{
    /// <summary>
    /// If an exception of this type is thrown in a Job's Perform method, 
    /// the entire batch of jobs is marked as failed. 
    ///
    /// The worker that was executing the batch stops and clears any
    /// buffered jobs (returns them to the broker). If there is another
    /// application instance running it will pick up the messages. 
    /// 
    /// If not, the worker will reconnect after 5 seconds and retry the job batch.
    /// </summary>
    public class BatchRolledBackException: Exception 
    {
        public BatchRolledBackException(): base()
        {
        }

        public BatchRolledBackException(string message): base(message)
        {
        }

        public BatchRolledBackException(Exception innerException) : base("Job batch rolled back", innerException)
        {
        }
    }
}
