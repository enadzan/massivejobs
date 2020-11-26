using System;
using System.Collections.Generic;
using System.Reflection;

namespace MassiveJobs.Core.Serialization
{
    using ToJobInfoDelegate = Func<object, string, IJobTypeProvider, JobInfo>;

    public abstract class BaseSerializer : IJobSerializer
    {
        private static readonly Dictionary<Type, Tuple<Type, ToJobInfoDelegate>> SerializationInfo = new Dictionary<Type, Tuple<Type, ToJobInfoDelegate>>();

        protected abstract object DeserializeEnvelope(ReadOnlySpan<byte> data, Type envelopeType);
        protected abstract byte[] SerializeEnvelope(Type argsType, SerializedEnvelope<object> envelope);

        public JobInfo Deserialize(ReadOnlySpan<byte> data, string argsTag, IJobTypeProvider typeProvider)
        {
            var argsType = typeProvider.TagToType(argsTag);

            Tuple<Type, ToJobInfoDelegate> info;

            lock (SerializationInfo)
            {
                if (!SerializationInfo.TryGetValue(argsType, out info))
                {
                    var serializedType = typeof(SerializedEnvelope<>).MakeGenericType(argsType);

                    // ReSharper disable once PossibleNullReferenceException
                    var methodInfo = typeof(BaseSerializer).GetMethod(nameof(GetToJobInfoDelegate), BindingFlags.Static | BindingFlags.NonPublic)
                        .MakeGenericMethod(argsType);

                    var jobDelegate = (ToJobInfoDelegate) methodInfo.Invoke(null, new object[] {argsType});

                    info = new Tuple<Type, ToJobInfoDelegate>(serializedType, jobDelegate);

                    SerializationInfo.Add(argsType, info);
                }
            }

            var serializedEnv = DeserializeEnvelope(data, info.Item1);

            return info.Item2(serializedEnv, argsTag, typeProvider);
        }

        public byte[] Serialize(JobInfo jobInfo, IJobTypeProvider typeProvider)
        {
            var serializedEnvelope = ToSerializedEnvelope(jobInfo, typeProvider);
            return SerializeEnvelope(jobInfo.ArgsType, serializedEnvelope);
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

        private static ToJobInfoDelegate GetToJobInfoDelegate<T>(Type argsType)
        {
            // ReSharper disable once PossibleNullReferenceException
            var methodInfo = typeof(BaseSerializer)
                .GetMethod(nameof(ToJobInfo), BindingFlags.Static | BindingFlags.NonPublic)
                .MakeGenericMethod(argsType);

            // Convert the slow MethodInfo into a fast, strongly typed, open delegate
            var func = (Func<SerializedEnvelope<T>, string, IJobTypeProvider, JobInfo>) Delegate.CreateDelegate(
                typeof(Func<SerializedEnvelope<T>, string, IJobTypeProvider, JobInfo>),
                methodInfo
            );

            // Now create a more weakly typed delegate which will call the strongly typed one
            return (env, argsTag, typeProvider) => func((SerializedEnvelope<T>) env, argsTag, typeProvider);
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
