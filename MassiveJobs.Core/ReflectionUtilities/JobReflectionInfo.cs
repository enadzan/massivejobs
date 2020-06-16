using System.Reflection;

namespace MassiveJobs.Core.ReflectionUtilities
{
    public class JobReflectionInfo
    {
        public ConstructorInfo Ctor1 { get; }
        public ConstructorInfo Ctor2 { get; }
        public MethodInfo Perf1 { get; }
        public MethodInfo Perf2 { get; }

        public JobReflectionInfo(ConstructorInfo ctor1, ConstructorInfo ctor2, MethodInfo perf1, MethodInfo perf2)
        {
            Ctor1 = ctor1;
            Ctor2 = ctor2;
            Perf1 = perf1;
            Perf2 = perf2;
        }
    }
}
