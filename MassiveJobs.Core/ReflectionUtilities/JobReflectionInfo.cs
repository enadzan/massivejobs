using System.Reflection;

namespace MassiveJobs.Core.ReflectionUtilities
{
    public class JobReflectionInfo
    {
        public ConstructorInfo Ctor { get; }
        public ConstructorType CtorType { get; }

        public MethodInfo PerfMethod { get; }
        public PerformMethodType PerfMethodType { get; }

        public JobReflectionInfo(ConstructorInfo ctor, ConstructorType ctorType, MethodInfo perfMethod, PerformMethodType perfMethodType)
        {
            Ctor = ctor;
            CtorType = ctorType;
            PerfMethod = perfMethod;
            PerfMethodType = perfMethodType;
        }
    }

    public enum ConstructorType
    {
        Other = 0,
        NoArgs = 1,
        NeedsPublisher = 2
    }

    public enum PerformMethodType
    {
        NoArgs = 0,
        NeedsArgs = 1,
        NeedsCancellationToken = 2,
        NeedsArgsAndCancellationToken = 3
    }

}
