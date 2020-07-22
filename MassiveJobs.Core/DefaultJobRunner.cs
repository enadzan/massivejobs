using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;

namespace MassiveJobs.Core
{
    public class DefaultJobRunner : IJobRunner
    {
        private readonly IJobLogger _logger;
        private readonly int _defaultJobTimeout;

        public DefaultJobRunner(IJobLogger logger, int defaultJobTimeoutMs = 5 * 1000)
        {
            _logger = logger ?? new DefaultLogger<DefaultJobRunner>();
            _defaultJobTimeout = defaultJobTimeoutMs;
        }

        public void RunJobs(IJobPublisher publisher, IReadOnlyList<JobInfo> jobs, IJobServiceScope serviceScope, CancellationToken cancellationToken)
        {
            for (var i = 0; i < jobs.Count; i++)
            {
                if (cancellationToken.IsCancellationRequested) break;
                Run(publisher, jobs[i], serviceScope, cancellationToken);
            }
        }

        private void Run(IJobPublisher publisher, JobInfo jobInfo, IJobServiceScope serviceScope, CancellationToken cancellationToken)
        {
            try
            {
                using (var timeoutTokenSource = new CancellationTokenSource(jobInfo.TimeoutMs ?? _defaultJobTimeout))
                {
                    var timeoutToken = timeoutTokenSource.Token;

                    using (var combinedTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutToken))
                    {
                        try
                        {
                            InvokePerform(publisher, jobInfo, serviceScope, combinedTokenSource.Token);
                        }
                        catch (OperationCanceledException)
                        {
                            if (timeoutToken.IsCancellationRequested) throw new OperationCanceledException("A job timed out.");
                            throw;
                        }
                    }
                }
            }
            catch (BatchRolledBackException)
            {
                // this exception type should be propagated to the worker to abort the whole batch
                throw; 
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed running job: {jobInfo.JobType} / {jobInfo.ArgsType} / {jobInfo.GroupKey}");
                publisher.RescheduleJob(jobInfo, ex);
            }
        }

        protected void InvokePerform(IJobPublisher publisher, JobInfo jobInfo, IJobServiceScope serviceScope, CancellationToken cancellationToken)
        {
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
                        job = reflectionInfo.Ctor.Invoke(new[] { publisher });
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

                object result;

                switch (reflectionInfo.PerfMethodType)
                {
                    case ReflectionUtilities.PerformMethodType.NoArgs:
                        result = reflectionInfo.PerfMethod.Invoke(job, null);
                        break;
                    case ReflectionUtilities.PerformMethodType.NeedsArgs:
                        result = reflectionInfo.PerfMethod.Invoke(job, new object[] { jobInfo.Args });
                        break;
                    case ReflectionUtilities.PerformMethodType.NeedsCancellationToken:
                        result = reflectionInfo.PerfMethod.Invoke(job, new object[] { cancellationToken });
                        break;
                    case ReflectionUtilities.PerformMethodType.NeedsArgsAndCancellationToken:
                        result = reflectionInfo.PerfMethod.Invoke(job, new object[] { jobInfo.Args, cancellationToken });
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(reflectionInfo.PerfMethodType));
                }

                if (result != null && result is Task taskResult)
                {
                    // All jobs in a batch are running in one service scope, which is why
                    // we are not running them in parallel (usually, scope level services
                    // like db connections cannot be shared between threads)
                    taskResult.Wait(cancellationToken);
                }
            }
            catch (TargetInvocationException ex)
            {
                ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
            }
        }
    }
}
