using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace MassiveJobs.Core
{
    public class DefaultJobRunner : IJobRunner
    {
        private readonly ILogger _logger;
        private readonly int _defaultJobTimeout;

        public DefaultJobRunner(ILogger logger, int defaultJobTimeoutMs = 5 * 1000)
        {
            _logger = logger;
            _defaultJobTimeout = defaultJobTimeoutMs;
        }

        public void RunJobs(IJobPublisher publisher, IEnumerable<JobInfo> jobs, IServiceScope serviceScope, CancellationToken cancellationToken)
        {
            foreach (var jobInfo in jobs)
            {
                Run(publisher, jobInfo, serviceScope, cancellationToken);
            }
        }

        private void Run(IJobPublisher publisher, JobInfo jobInfo, IServiceScope serviceScope, CancellationToken cancellationToken)
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
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed running job: {jobInfo.JobType} / {jobInfo.ArgsType} / {jobInfo.PeriodicRunInfo?.RunId}");
                publisher.RescheduleJob(jobInfo, ex);
            }
        }

        private void InvokePerform(IJobPublisher publisher, JobInfo jobInfo, IServiceScope serviceScope, CancellationToken cancellationToken)
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
                    job = serviceScope.GetService(jobInfo.JobType);
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
                taskResult.Wait(cancellationToken);
            }
        }
    }
}
