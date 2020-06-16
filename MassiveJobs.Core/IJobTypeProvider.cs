using System;

namespace MassiveJobs.Core
{
    public interface IJobTypeProvider
    {
        Type TagToType(string tag);
        string TypeToTag(Type type);
    }
}
