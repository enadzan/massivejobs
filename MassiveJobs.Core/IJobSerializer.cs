using System;

namespace MassiveJobs.Core
{
    public interface IJobSerializer
    {
        byte[] Serialize(JobInfo jobInfo, IJobTypeProvider typeProvider);
        JobInfo Deserialize(ReadOnlySpan<byte> data, string argsTag, IJobTypeProvider typeProvider);
    }
}
