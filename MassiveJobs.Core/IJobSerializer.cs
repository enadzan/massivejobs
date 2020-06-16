using System;

namespace MassiveJobs.Core
{
    public interface IJobSerializer
    {
        ReadOnlyMemory<byte> Serialize(JobInfo jobInfo, IJobTypeProvider typeProvider);
        JobInfo Deserialize(ReadOnlySpan<byte> data, string argsTag, IJobTypeProvider typeProvider);

        /// <summary>
        /// Used in exceptions to log human readable job data
        /// </summary>
        /// <param name="job"></param>
        /// <returns></returns>
        string ToJson(ReadOnlySpan<byte> jobData);
    }
}
