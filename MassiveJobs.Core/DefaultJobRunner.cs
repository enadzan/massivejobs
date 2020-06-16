using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace MassiveJobs.Core
{
    public class DefaultJobRunner : IJobRunner
    {
        private readonly int _defaultJobTimeout;

        public DefaultJobRunner(int defaultJobTimeoutMs = 5 * 1000)
        {
            _defaultJobTimeout = defaultJobTimeoutMs;
        }

        public async Task RunJobs(IJobPublisher publisher, IEnumerable<JobInfo> jobs, IServiceScope serviceScope, CancellationToken cancellationToken)
        {
            var runningTasks = new List<Task>();
            var runningJobs = new List<JobInfo>();

            foreach (var jobInfo in jobs)
            {
                runningJobs.Add(jobInfo);
                runningTasks.Add(Run(publisher, jobInfo, serviceScope, cancellationToken));
            }

            while (runningTasks.Count > 0)
            {
                var completedTask = await Task.WhenAny(runningTasks)
                    .ConfigureAwait(false);

                var position = runningTasks.IndexOf(completedTask);
                if (position < 0) continue; // should not happen

                if (completedTask.IsCanceled || completedTask.IsFaulted)
                {
                    publisher.RescheduleJob(runningJobs[position], completedTask.Exception);
                }

                runningTasks.RemoveAt(position);
                runningJobs.RemoveAt(position);
            }
        }

        private async Task Run(IJobPublisher publisher, JobInfo jobInfo, IServiceScope serviceScope, CancellationToken cancellationToken)
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
                            await InvokePerform(publisher, jobInfo, serviceScope, combinedTokenSource.Token)
                                .ConfigureAwait(false);
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
                publisher.RescheduleJob(jobInfo, ex);
            }
        }

        private Task InvokePerform(IJobPublisher publisher, JobInfo jobInfo, IServiceScope serviceScope, CancellationToken cancellationToken)
        {
            var reflectionInfo = ReflectionUtilities.ReflectionCache.GetJobReflectionInfo(jobInfo.JobType, jobInfo.ArgsType);

            object job;

            if (reflectionInfo.Ctor1 != null)
            {
                job = reflectionInfo.Ctor1.Invoke(new[] { publisher });
            }
            else if (reflectionInfo.Ctor2 != null)
            {
                job = reflectionInfo.Ctor2.Invoke(null);
            }
            else
            {
                job = serviceScope.ServiceProvider.GetService(jobInfo.JobType);
                if (job == null)
                {
                    throw new Exception($"Job type {jobInfo.JobType} is not registered in service scope and appropriate constructor does not exist!");
                }
            }

            object result;

            if (reflectionInfo.Perf1 != null)
            {
                result = reflectionInfo.Perf1.Invoke(job, new[] { jobInfo.Args, cancellationToken });
            }
            else
            {
                result = reflectionInfo.Perf2.Invoke(job, new[] { jobInfo.Args });
            }

            return result == null ? Task.CompletedTask : (Task)result;
        }
    }
}
