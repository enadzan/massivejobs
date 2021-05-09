using System;

namespace MassiveJobs.Core
{
    public interface ITimeProvider
    {
        DateTime GetCurrentTimeUtc();
    }
}
