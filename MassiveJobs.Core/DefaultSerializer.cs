using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using System.Text.Json;

namespace MassiveJobs.Core
{
    public class DefaultSerializer : IJobSerializer
    {
        private readonly static JsonSerializerOptions Options = new JsonSerializerOptions { IgnoreNullValues = true, IgnoreReadOnlyProperties = true };

        private readonly static Dictionary<Type, Tuple<Type, MethodInfo>> SerializationInfo = new Dictionary<Type, Tuple<Type, MethodInfo>>();

        public JobInfo Deserialize(ReadOnlySpan<byte> data, string argsTag, IJobTypeProvider typeProvider)
        {
            var argsType = typeProvider.TagToType(argsTag);

            Tuple<Type, MethodInfo> info;

            lock (SerializationInfo)
            {
                if (!SerializationInfo.TryGetValue(argsType, out info))
                {
                    var serializedType = typeof(SerializedEnvelope<>).MakeGenericType(argsType);

                    var methodInfo = typeof(DefaultSerializer).GetMethod(nameof(TojobInfo), BindingFlags.Static | BindingFlags.NonPublic)
                        .MakeGenericMethod(argsType);

                    info = new Tuple<Type, MethodInfo>(serializedType, methodInfo);

                    SerializationInfo.Add(argsType, info);
                }
            }

            var serializedEnv = JsonSerializer.Deserialize(data, info.Item1, Options);
            return (JobInfo)info.Item2.Invoke(null, new[] { serializedEnv, argsTag, typeProvider });
        }

        public ReadOnlyMemory<byte> Serialize(JobInfo jobInfo, IJobTypeProvider typeProvider)
        {
            var serializedEnvelope = ToSerializedEnvelope(jobInfo, typeProvider);
            return SerializeObject(serializedEnvelope);
        }

        /// <summary>
        /// Used in exceptions to log human readable job data
        /// </summary>
        /// <param name="job"></param>
        /// <returns></returns>
        public string ToJson(ReadOnlySpan<byte> jobData)
        {
            return Encoding.UTF8.GetString(jobData.ToArray());
        }

        /// <summary>
        /// We will use this when we specifically want JSON (such as when publishing ::masivejobs::stats)
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        internal static byte[] SerializeObject(object obj)
        {
            var json = JsonSerializer.Serialize(obj, Options);
            return Encoding.UTF8.GetBytes(json);
        }

        private static SerializedEnvelope<object> ToSerializedEnvelope(JobInfo jobInfo, IJobTypeProvider typeProvider)
        {
            var jobTag = typeProvider.TypeToTag(jobInfo.JobType);

            return new SerializedEnvelope<object>
            {
                J = jobTag,
                A = jobInfo.Args,
                E = jobInfo.Err,
                At = jobInfo.RunAtUtc,
                R = jobInfo.Retries,
                T = jobInfo.TimeoutMs,
                G = jobInfo.GroupKey
            };
        }

        private static JobInfo TojobInfo<T>(SerializedEnvelope<T> serialized, string argsTag, IJobTypeProvider typeProvider)
        {
            var argsType = typeProvider.TagToType(argsTag);
            var jobType = typeProvider.TagToType(serialized.J);

            return new JobInfo
            {
                JobType = jobType,
                ArgsType = argsType,
                Args = serialized.A,
                Err = serialized.E,
                RunAtUtc = serialized.At,
                Retries = serialized.R,
                TimeoutMs = serialized.T,
                GroupKey = serialized.G
            };
        }

        private class SerializedEnvelope<TArgs>
        {
            public string J { get; set; }
            public TArgs A { get; set; }
            public DateTime? At { get; set; }

            public string O { get; set; }
            public int? R { get; set; }
            public string E { get; set; }
            public int? T { get; set; }
            public string G { get; set; }
        }
    }
}
