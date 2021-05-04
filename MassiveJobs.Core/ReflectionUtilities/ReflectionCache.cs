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

                ConstructorInfo c = null;
                ConstructorType cType = ConstructorType.Other;

                foreach (var ctorArg in ctorArgs)
                {
                    c = jobType.GetConstructor(new[] { ctorArg });

                    if (c != null)
                    {
                        cType = ConstructorType.NeedsPublisher;
                        break;
                    }
                }

                if (c == null)
                {
                    c = jobType.GetConstructor(Type.EmptyTypes);
                    if (c != null)
                    {
                        cType = ConstructorType.NoArgs;
                    }
                }

                if (c == null)
                {
                    var publicCtors = jobType.GetConstructors();
                    if (publicCtors.Length > 0)
                    {
                        c = publicCtors[0];
                    }
                }

                if (c == null) throw new Exception($"Invalid type '{jobType}' for a job. Public constructor not found");

                MethodInfo m;
                PerformMethodType mType;

                if (argsType == typeof(VoidArgs))
                {
                    mType = PerformMethodType.NeedsCancellationToken;
                    m = jobType.GetMethod(methodName, new[] { typeof(CancellationToken) });

                    if (m == null)
                    {
                        mType = PerformMethodType.NoArgs;
                        m = jobType.GetMethod(methodName, Type.EmptyTypes);

                        if (m == null)
                        {
                            throw new Exception($"Invalid type '{jobType}' for a job. {methodName} method with appropriate arguments not found.");
                        }
                    }
                }
                else
                {
                    mType = PerformMethodType.NeedsArgsAndCancellationToken;
                    m = jobType.GetMethod(methodName, new[] { argsType, typeof(CancellationToken) });

                    if (m == null)
                    {
                        mType = PerformMethodType.NeedsArgs;
                        m = jobType.GetMethod(methodName, new[] { argsType });

                        if (m == null)
                        {
                            throw new Exception($"Invalid type '{jobType}' for a job. {methodName} method with appropriate arguments not found.");
                        }
                    }
                }

                var useTransProp = jobType.GetProperty("UseTransaction", typeof(bool));

                reflectionInfo = new JobReflectionInfo(c, cType, m, mType, useTransProp);

                argTypeToInfoMap.Add(argsType, reflectionInfo);

                return reflectionInfo;
            }
        }
    }
}
