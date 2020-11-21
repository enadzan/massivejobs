using System;
using System.Collections.Generic;
using System.Reflection;

namespace MassiveJobs.Core.Serialization
{
    public abstract class BaseSerializer : IJobSerializer
    {
        private static readonly Dictionary<Type, Tuple<Type, MethodInfo>> SerializationInfo = new Dictionary<Type, Tuple<Type, MethodInfo>>();

        protected abstract object DeserializeEnvelope(ReadOnlySpan<byte> data, Type envelopeType);
        protected abstract byte[] SerializeEnvelope(SerializedEnvelope<object> envelope);

        public JobInfo Deserialize(ReadOnlySpan<byte> data, string argsTag, IJobTypeProvider typeProvider)
        {
            var argsType = typeProvider.TagToType(argsTag);

            Tuple<Type, MethodInfo> info;

            lock (SerializationInfo)
            {
                if (!SerializationInfo.TryGetValue(argsType, out info))
                {
                    var serializedType = typeof(SerializedEnvelope<>).MakeGenericType(argsType);

                    // ReSharper disable once PossibleNullReferenceException
                    var methodInfo = typeof(BaseSerializer).GetMethod(nameof(ToJobInfo), BindingFlags.Static | BindingFlags.NonPublic)
                        .MakeGenericMethod(argsType);

                    info = new Tuple<Type, MethodInfo>(serializedType, methodInfo);

                    SerializationInfo.Add(argsType, info);
                }
            }

            var serializedEnv = DeserializeEnvelope(data, info.Item1);

            return (JobInfo)info.Item2.Invoke(null, new[] { serializedEnv, argsTag, typeProvider });
        }

        public byte[] Serialize(JobInfo jobInfo, IJobTypeProvider typeProvider)
        {
            var serializedEnvelope = ToSerializedEnvelope(jobInfo, typeProvider);
            return SerializeEnvelope(serializedEnvelope);
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
                G = jobInfo.GroupKey,
                P = jobInfo.PeriodicRunInfo
            };
        }

        private static JobInfo ToJobInfo<T>(SerializedEnvelope<T> serialized, string argsTag, IJobTypeProvider typeProvider)
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
                GroupKey = serialized.G,
                PeriodicRunInfo = serialized.P
            };
        }

        protected class SerializedEnvelope<TArgs>
        {
            public string J { get; set; }
            public TArgs A { get; set; }
            public DateTime? At { get; set; }

            public int? R { get; set; }
            public string E { get; set; }
            public int? T { get; set; }
            public string G { get; set; }

            public PeriodicRunInfo P { get; set; }
        }
    }
}
