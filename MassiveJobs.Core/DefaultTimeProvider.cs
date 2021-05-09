using System;

namespace MassiveJobs.Core
{
    public class DefaultTimeProvider : ITimeProvider
    {
        public DateTime GetCurrentTimeUtc()
        {
            return DateTime.UtcNow;
        }
    }
}
