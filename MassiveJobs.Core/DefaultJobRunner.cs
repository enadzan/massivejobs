using System;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;

using MassiveJobs.Core.DependencyInjection;

namespace MassiveJobs.Core
{
    public class DefaultJobRunner : IJobRunner
    {
        private readonly IJobLogger<DefaultJobRunner> _logger;
        private const int DefaultJobTimeoutMs = 5 * 1000;

        public DefaultJobRunner(IJobLogger<DefaultJobRunner> logger)
        {
            _logger = logger;
        }

        public void RunJob(IJobPublisher publisher, IMessageReceiver receiver, JobInfo jobInfo, ulong deliveryTag, IJobServiceScope serviceScope, CancellationToken cancellationToken)
        {
            try
            {
                using (var timeoutTokenSource = new CancellationTokenSource(jobInfo.TimeoutMs ?? DefaultJobTimeoutMs))
                {
                    var timeoutToken = timeoutTokenSource.Token;

                    using (var combinedTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutToken))
                    {
                        try
                        {
                            InvokePerform(publisher, receiver, jobInfo, deliveryTag, serviceScope, combinedTokenSource.Token);
                        }
                        catch (OperationCanceledException)
                        {
                            if (timeoutToken.IsCancellationRequested) throw new OperationCanceledException("A job timed out.");
                            throw;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed running job: {jobInfo.JobType} / {jobInfo.ArgsType} / {jobInfo.GroupKey}");

                publisher.RescheduleJob(jobInfo, ex);

                receiver.AckBatchMessageProcessed(serviceScope, deliveryTag);
            }
        }

        protected void InvokePerform(IJobPublisher publisher, IMessageReceiver receiver, JobInfo jobInfo, ulong deliveryTag, IJobServiceScope serviceScope, CancellationToken cancellationToken)
        {
            IBrokerTransaction tx = null;

            try
            {
                var reflectionInfo = ReflectionUtilities.ReflectionCache.GetJobReflectionInfo(jobInfo.JobType, jobInfo.ArgsType);

                object job;

                switch (reflectionInfo.CtorType)
                {
                    case ReflectionUtilities.ConstructorType.NoArgs:
                        job = reflectionInfo.Ctor.Invoke(null);
                        break;
                    case ReflectionUtilities.ConstructorType.NeedsPublisher:
                        job = reflectionInfo.Ctor.Invoke(new object[] { publisher });
                        break;
                    default:
                        var parametersInfo = reflectionInfo.Ctor.GetParameters();
                        var parameters = new object[parametersInfo.Length];

                        for (var i = 0; i < parametersInfo.Length; i++)
                        {
                            if (parametersInfo[i].IsOut) throw new Exception("Out parameters are not supported.");
                            parameters[i] = serviceScope.GetRequiredService(parametersInfo[i].ParameterType);
                        }

                        job = reflectionInfo.Ctor.Invoke(parameters);
                        break;
                }

                if (job == null)
                {
                    throw new Exception($"Job type {jobInfo.JobType} is not registered in service scope and appropriate constructor does not exist!");
                }

                if ((bool)reflectionInfo.UseTransactionGetter(job))
                {
                    tx = receiver.BeginTransaction(serviceScope);
                }

                object result;

                switch (reflectionInfo.PerfMethodType)
                {
                    case ReflectionUtilities.PerformMethodType.NoArgs:
                        result = reflectionInfo.PerformDelegate1(job);
                        break;
                    case ReflectionUtilities.PerformMethodType.NeedsArgs:
                        result = reflectionInfo.PerformDelegate2(job, jobInfo.Args);
                        break;
                    case ReflectionUtilities.PerformMethodType.NeedsCancellationToken:
                        result = reflectionInfo.PerformDelegate3(job, cancellationToken);
                        break;
                    case ReflectionUtilities.PerformMethodType.NeedsArgsAndCancellationToken:
                        result = reflectionInfo.PerformDelegate4(job, jobInfo.Args, cancellationToken);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(reflectionInfo.PerfMethodType));
                }

                if (result != null && result is Task taskResult)
                {
                    taskResult.Wait(cancellationToken);
                }

                receiver.AckBatchMessageProcessed(serviceScope, deliveryTag);

                tx?.Commit();
            }
            catch (TargetInvocationException ex)
            {
                try
                {
                    tx?.Rollback();
                }
                catch (Exception rollbackEx)
                {
                    _logger.LogError(rollbackEx, "Rollback failed");
                }

                if (ex.InnerException == null) throw;
                ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
            }
            finally
            {
                tx.SafeDispose(_logger);
            }
        }
    }
}
