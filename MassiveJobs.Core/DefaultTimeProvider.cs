using System;

namespace MassiveJobs.Core
{
    class DefaultTimeProvider : ITimeProvider
    {
        public DateTime GetCurrentTimeUtc()
        {
            return DateTime.UtcNow;
        }
    }
}
