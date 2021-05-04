using System;
using System.Reflection;
using System.Threading;

namespace MassiveJobs.Core.ReflectionUtilities
{
    public class JobReflectionInfo
    {
        /// <summary>
        /// Calls "UseTransaction" property getter on the job type, if such property exists.
        /// Otherwise, returns false.
        /// </summary>
        public Func<object, object> UseTransactionGetter { get; }

        public ConstructorInfo Ctor { get; }
        public ConstructorType CtorType { get; }

        public PerformMethodType PerfMethodType { get; }

        public Func<object, object> PerformDelegate1 { get; }
        public Func<object, object, object> PerformDelegate2 { get; }
        public Func<object, CancellationToken, object> PerformDelegate3 { get; }
        public Func<object, object, CancellationToken, object> PerformDelegate4 { get; }

        public JobReflectionInfo(ConstructorInfo ctor, ConstructorType ctorType, MethodInfo perfMethod,
            PerformMethodType perfMethodType, PropertyInfo useTransactionProperty)
        {
            Ctor = ctor;
            CtorType = ctorType;
            PerfMethodType = perfMethodType;

            if (useTransactionProperty != null)
            {
                UseTransactionGetter = (Func<object, object>) GetDelegateFactory(
                    nameof(CreateDelegate1),
                    useTransactionProperty.DeclaringType,
                    useTransactionProperty.PropertyType
                    )
                    .Invoke(null, new object[] { useTransactionProperty.GetMethod });
            }
            else
            {
                UseTransactionGetter = _ => false;
            }

            switch (PerfMethodType)
            {
                case PerformMethodType.NoArgs:
                    PerformDelegate1 = (Func<object, object>) GetDelegateFactory(
                            nameof(CreateDelegate1),
                            perfMethod.DeclaringType, 
                            perfMethod.ReturnType
                        )
                        .Invoke(null, new object[] {perfMethod});
                    break;
                case PerformMethodType.NeedsArgs:
                    PerformDelegate2 = (Func<object, object, object>) GetDelegateFactory(
                            nameof(CreateDelegate2),
                            perfMethod.DeclaringType, 
                            perfMethod.GetParameters()[0].ParameterType, 
                            perfMethod.ReturnType
                        )
                        .Invoke(null, new object[] {perfMethod});
                    break;
                case PerformMethodType.NeedsCancellationToken:
                    PerformDelegate3 = (Func<object, CancellationToken, object>) GetDelegateFactory(
                            nameof(CreateDelegate3),
                            perfMethod.DeclaringType, 
                            perfMethod.ReturnType
                        )
                        .Invoke(null, new object[] {perfMethod});
                    break;
                case PerformMethodType.NeedsArgsAndCancellationToken:
                    PerformDelegate4 = (Func<object, object, CancellationToken, object>) GetDelegateFactory(
                            nameof(CreateDelegate4),
                            perfMethod.DeclaringType, 
                            perfMethod.GetParameters()[0].ParameterType, 
                            perfMethod.ReturnType
                        )
                        .Invoke(null, new object[] {perfMethod});
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(PerfMethodType));
            }
        }

        private static MethodInfo GetDelegateFactory(string name, params Type[] typeArguments)
        {
            if (typeArguments[typeArguments.Length - 1] == typeof(void))
            {
                name += "Void";
                var originalTypeArguments = typeArguments;

                typeArguments = new Type[typeArguments.Length - 1];
                for (var i = 0; i < typeArguments.Length; i++)
                {
                    typeArguments[i] = originalTypeArguments[i];
                }
            }

            return typeof(JobReflectionInfo).GetMethod(name, BindingFlags.Static | BindingFlags.NonPublic)?
                       .MakeGenericMethod(typeArguments)
                   ?? throw new Exception($"Cannot find method: {name}");
        }

        private static Func<object, object> CreateDelegate1<TTarget, TReturn>(MethodInfo methodInfo)
        {
            var strongD = (Func<TTarget, TReturn>) Delegate.CreateDelegate(typeof(Func<TTarget, TReturn>), methodInfo);
            return target => strongD((TTarget) target);
        }

        private static Func<object, object> CreateDelegate1Void<TTarget>(MethodInfo methodInfo)
        {
            var strongD = (Action<TTarget>) Delegate.CreateDelegate(typeof(Action<TTarget>), methodInfo);

            return target =>
            {
                strongD((TTarget) target);
                return null;
            };
        }

        private static Func<object, object, object> CreateDelegate2<TTarget, TParam, TReturn>(MethodInfo methodInfo)
        {
            var strongD = (Func<TTarget, TParam, TReturn>) Delegate
                .CreateDelegate(typeof(Func<TTarget, TParam, TReturn>), methodInfo);

            return (target, param) => strongD((TTarget) target, (TParam) param);
        }

        private static Func<object, object, object> CreateDelegate2Void<TTarget, TParam>(MethodInfo methodInfo)
        {
            var strongD = (Action<TTarget, TParam>) Delegate
                .CreateDelegate(typeof(Action<TTarget, TParam>), methodInfo);

            return (target, param) =>
            {
                strongD((TTarget) target, (TParam) param);
                return null;
            };
        }

        private static Func<object, CancellationToken, object> CreateDelegate3<TTarget, TReturn>(MethodInfo methodInfo)
        {
            var strongD = (Func<TTarget, CancellationToken, TReturn>) Delegate
                .CreateDelegate(typeof(Func<TTarget, CancellationToken, TReturn>), methodInfo);

            return (target, ct) => strongD((TTarget) target, ct);
        }

        private static Func<object, CancellationToken, object> CreateDelegate3Void<TTarget, TReturn>(MethodInfo methodInfo)
        {
            var strongD = (Action<TTarget, CancellationToken>) Delegate
                .CreateDelegate(typeof(Action<TTarget, CancellationToken>), methodInfo);

            return (target, ct) =>
            {
                strongD((TTarget) target, ct);
                return null;
            };
        }

        private static Func<object, object, CancellationToken, object> CreateDelegate4<TTarget, TParam, TReturn>(MethodInfo methodInfo)
        {
            var strongD = (Func<TTarget, TParam, CancellationToken, TReturn>) Delegate
                .CreateDelegate(typeof(Func<TTarget, TParam, CancellationToken, TReturn>), methodInfo);

            return (target, param, ct) => strongD((TTarget) target, (TParam) param, ct);
        }

        private static Func<object, object, CancellationToken, object> CreateDelegate4Void<TTarget, TParam>(MethodInfo methodInfo)
        {
            var strongD = (Action<TTarget, TParam, CancellationToken>) Delegate
                .CreateDelegate(typeof(Action<TTarget, TParam, CancellationToken>), methodInfo);

            return (target, param, ct) =>
            {
                strongD((TTarget) target, (TParam) param, ct);
                return null;
            };
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
