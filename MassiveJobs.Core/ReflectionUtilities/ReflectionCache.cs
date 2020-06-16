using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;

namespace MassiveJobs.Core.ReflectionUtilities
{
    using InfoPerArgType = Dictionary<Type, JobReflectionInfo>;

    public static class ReflectionCache
    {
        private static readonly Dictionary<string, Dictionary<Type, InfoPerArgType>> CachePerMethod = new Dictionary<string, Dictionary<Type, InfoPerArgType>>();

        public static JobReflectionInfo GetJobReflectionInfo(Type jobType, Type argsType)
        {
            return GetReflectionInfo(jobType, argsType, "Perform", typeof(IJobPublisher));
        }

        public static JobReflectionInfo GetReflectionInfo(Type jobType, Type argsType, string methodName, params Type[] ctorArgs)
        {
            Dictionary<Type, InfoPerArgType> cache;

            lock (CachePerMethod)
            {
                if (!CachePerMethod.TryGetValue(methodName, out cache))
                {
                    cache = new Dictionary<Type, InfoPerArgType>();
                    CachePerMethod.Add(methodName, cache);
                }
            }

            lock (cache) 
            { 
                JobReflectionInfo reflectionInfo = null;

                if (cache.TryGetValue(jobType, out var argTypeToInfoMap))
                {
                    if (argTypeToInfoMap.TryGetValue(argsType, out reflectionInfo))
                    {
                        return reflectionInfo;
                    }
                }
                else
                {
                    argTypeToInfoMap = new InfoPerArgType();
                    cache.Add(jobType, argTypeToInfoMap);
                }

                ConstructorInfo c1 = null, c2 = null;
                MethodInfo m1 = null, m2 = null;

                foreach (var ctorArg in ctorArgs)
                {
                    c1 = jobType.GetConstructor(new[] { typeof(IJobPublisher) });
                    if (c1 != null) break;
                }

                if (c1 == null)
                {
                    c2 = jobType.GetConstructor(Type.EmptyTypes);
                }

                m1 = jobType.GetMethod(methodName, new[] { argsType, typeof(CancellationToken) });
                if (m1 == null)
                {
                    m2 = jobType.GetMethod(methodName, new[] { argsType });
                    if (m2 == null)
                    {
                        throw new Exception($"Invalid type '{jobType}' for a job. {methodName} method not found.");
                    }
                }

                reflectionInfo = new JobReflectionInfo(c1, c2, m1, m2);
                argTypeToInfoMap.Add(argsType, reflectionInfo);

                return reflectionInfo;
            }
        }
    }
}
