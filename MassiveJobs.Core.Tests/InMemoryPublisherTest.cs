using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Threading;
using System.Threading.Tasks;

using MassiveJobs.Core.Memory;

namespace MassiveJobs.Core.Tests
{
    [TestClass]
    public class InMemoryPublisherTest
    {
        private static int _performCount;

        private readonly MassiveJobsSettings _settings = new MassiveJobsSettings
        {
            ImmediateWorkersCount = 2,
            ScheduledWorkersCount = 2,
            PeriodicWorkersCount = 2
        };

        private InMemoryMessages _messages;

        private IWorkerCoordinator _workerCoordinator;
        private IJobPublisher _jobPublisher;
        private MassiveJobsMediator _jobs;

        [TestInitialize]
        public void TestInit()
        {
            _performCount = 0;

            _messages = new InMemoryMessages();

            var messagePublisher = new InMemoryMessagePublisher(_settings, _messages);
            var messageConsumer = new InMemoryMessageConsumer(_messages);

            var scopeFactory = new DefaultServiceScopeFactory(
                _settings,
                messagePublisher,
                messageConsumer
            );

            _jobPublisher = scopeFactory.CreateScope().GetRequiredService<IJobPublisher>();
            _workerCoordinator = new WorkerCoordinator(scopeFactory, _settings, messageConsumer);

            _jobs = new MassiveJobsMediator(_jobPublisher, _workerCoordinator);
        }

        [TestCleanup]
        public void TestCleanup()
        {
            _jobPublisher.SafeDispose();
            _workerCoordinator.SafeDispose();
        }

        [TestMethod]
        public void TestPublishInc()
        {
            _jobs.StartJobWorkers();

            _jobs.Publish<MockJob, bool>(true);

            Thread.Sleep(100);

            Assert.AreEqual(1, _performCount);
        }

        [TestMethod]
        public void TestPublishWithDelay()
        {
            _jobs.StartJobWorkers();

            _jobs.Publish<MockJob, bool>(true, TimeSpan.FromSeconds(2));
            _jobs.Publish<MockJob, bool>(true, TimeSpan.FromSeconds(2));

            Thread.Sleep(1000);

            Assert.AreEqual(0, _performCount);

            Thread.Sleep(1500);

            Assert.AreEqual(2, _performCount);
        }

        [TestMethod]
        public void TestPublishDec()
        {
            _jobs.StartJobWorkers();

            _jobs.Publish<MockJob, bool>(false);

            Thread.Sleep(100);

            Assert.AreEqual(-1, _performCount);
        }

        [TestMethod]
        public void TestFailedJobs()
        {
            _jobs.StartJobWorkers();

            _jobs.Publish<MockJob, bool>(true);
            _jobs.Publish<MockJobFailed, bool>(true);

            Thread.Sleep(100);

            Assert.AreEqual(1, _performCount);
            Assert.AreEqual(1, _messages.GetCount(_settings.ErrorQueueName));
        }

        [TestMethod]
        public void TestFailedAsyncJobs()
        {
            _jobs.StartJobWorkers();

            _jobs.Publish<MockJob, bool>(true);
            _jobs.Publish<MockAsyncJobFailed>();

            Thread.Sleep(100);

            Assert.AreEqual(1, _performCount);
            Assert.AreEqual(1, _messages.GetCount(_settings.ErrorQueueName));
        }

        [TestMethod]
        public void TestCanceledAsyncJobs()
        {
            _jobs.StartJobWorkers();

            _jobs.Publish<MockJob, bool>(true);
            _jobs.Publish<MockAsyncJobCanceled>();

            Thread.Sleep(200);

            Assert.AreEqual(1, _performCount);
            Assert.AreEqual(1,  _messages.GetCount(_settings.ErrorQueueName));
        }

        [TestMethod]
        public void TestScheduleParallel()
        {
            _jobs.StartJobWorkers();

            for (var i = 0; i < 10000; i++)
            {
                _jobs.Publish<MockJob, bool>(true);
            }

            Thread.Sleep(500);

            Assert.AreEqual(10000, _performCount);
        }

        [TestMethod]
        public void TestScheduleWithTimeoutDefault()
        {
            _jobs.StartJobWorkers();

            _jobs.Publish<LongRunningJobAsync, int>(6000);

            Thread.Sleep(6000);

            Assert.AreEqual(0, _performCount);
            Assert.AreEqual(1, _messages.GetCount(_settings.ErrorQueueName));
        }

        [TestMethod]
        public void TestScheduleWithTimeoutCustom()
        {
            _jobs.StartJobWorkers();

            _jobs.Publish<LongRunningJobAsync, int>(2000, null, 1000);

            Thread.Sleep(2000);

            Assert.AreEqual(0, _performCount);
            Assert.AreEqual(1, _messages.GetCount(_settings.ErrorQueueName));
        }
        
        [TestMethod]
        public void TestScheduleWithCancelledJobs()
        {
            _jobs.StartJobWorkers();

            _jobs.Publish<LongRunningJobAsync, int>(2000);

            _jobs.StopJobWorkers();

            Assert.AreEqual(0, _performCount);

            var remainingCount = 0;

            for (var i = 0; i < _settings.ImmediateWorkersCount; i++)
            {
                remainingCount += _messages.GetCount(string.Format(_settings.ImmediateQueueNameTemplate, i));
            }

            Assert.AreEqual(1, remainingCount);
        }

        [TestMethod]
        public void TestPeriodicJob()
        {
            _jobs.StartJobWorkers();

            _jobs.PublishPeriodic<MockJob, bool>(true, "test_job", 1);

            Thread.Sleep(3500);

            Assert.AreEqual(3, _performCount);
        }

        [TestMethod]
        public void TestPeriodicJobWithEndTime()
        {
            _jobs.StartJobWorkers();

            _jobs.PublishPeriodic<MockJob, bool>(true, "test_job", 1, null, DateTime.UtcNow.AddMilliseconds(4500));

            Thread.Sleep(6000);

            Assert.AreEqual(4, _performCount);
        }

        [TestMethod]
        public void TestCronJobWithEndTime()
        {
            _jobs.StartJobWorkers();

            _jobs.PublishPeriodic<MockJob>("test_job", "0/2 * * ? * *", null, null, DateTime.UtcNow.AddSeconds(4));

            Thread.Sleep(6000);

            Assert.IsTrue(_performCount == 2);
        }

        [TestMethod]
        public void TestPeriodicJobCancelling()
        {
            _jobs.StartJobWorkers();

            _jobs.PublishPeriodic<MockJob, bool>(true, "test_job", 1);

            Thread.Sleep(3500);

            _jobs.CancelPeriodic<MockJob, bool>(true, "test_job");

            Thread.Sleep(1000);

            Assert.AreEqual(3, _performCount);
        }

        [TestMethod]
        public void TestBatchRollback()
        {
            BatchFailingJob.Failed = false;

            _jobs.Publish<BatchFailingJob, bool>(false); // worker #1, job #1
            _jobs.Publish<BatchFailingJob, bool>(false); // worker #2, job #1
            _jobs.Publish<BatchFailingJob, bool>(true); // worker #1, job #2, fail this, once 
            _jobs.Publish<BatchFailingJob, bool>(false); // worker #2, job #2

            _jobs.StartJobWorkers();

            Thread.Sleep(100);

            // Since there are two workers, two jobs will go to each worker.
            // Workers will executed jobs in batches. First batch will fail (on second job), the second batch will succeed.
            Assert.AreEqual(3, _performCount);

            Thread.Sleep(5000);

            // The failed batch will be retried (both jobs in a batch)
            Assert.AreEqual(5, _performCount);
        }

        private class MockJob
        {
            public void Perform()
            {
                Interlocked.Increment(ref _performCount);
            }

            public void Perform(bool increment)
            {
                if (increment) Interlocked.Increment(ref _performCount);
                else Interlocked.Decrement(ref _performCount);
            }
        }

        private class MockJobFailed
        {
            public static bool ShoudFail { get; set; } = true;

            public void Perform(bool _)
            {
                if (ShoudFail)
                {
                    ShoudFail = false;
                    throw new Exception("Testing error in async job");
                }
            }
        }

        private class MockAsyncJobFailed
        {
            public async Task Perform(CancellationToken cancellationToken)
            {
                await Task.Delay(10, cancellationToken);

                throw new Exception("Testing error in async job");
            }
        }

        private class MockAsyncJobCanceled
        {
            public async Task Perform(CancellationToken cancellationToken)
            {
                await Task.Delay(100, cancellationToken);
                throw new OperationCanceledException();
            }
        }

        private class LongRunningJobAsync
        {
            public async Task Perform(int delayMs, CancellationToken cancellationToken)
            {
                await Task.Delay(delayMs, cancellationToken);
                Interlocked.Increment(ref _performCount);
            }
        }

        private class BatchFailingJob
        {
            public static bool Failed;

            public void Perform(bool fail)
            {
                if (fail && !Failed)
                {
                    Failed = true;
                    throw new BatchRolledBackException();
                }

                Interlocked.Increment(ref _performCount);
            }
        }
    }
}
