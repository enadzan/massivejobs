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
            ScheduledWorkersCount = 0,
            PeriodicWorkersCount = 1
        };

        private InMemoryMessages _messages;
        private Jobs _jobs;

        [TestInitialize]
        public void TestInit()
        {
            _performCount = 0;

            _messages = new InMemoryMessages();

            var messagePublisher = new InMemoryMessagePublisher(_settings, _messages);
            var messageConsumer = new InMemoryMessageConsumer(_messages);

            var scopeFactory = new DefaultServiceScopeFactory(_settings);

            scopeFactory.ServiceCollection.AddSingleton<IMessagePublisher>(messagePublisher);
            scopeFactory.ServiceCollection.AddSingleton<IMessageConsumer>(messageConsumer);
            scopeFactory.ServiceCollection.AddSingleton<IWorkerCoordinator>(
                new WorkerCoordinator(_settings, messageConsumer, scopeFactory, _settings.LoggerFactory)
            );

            _jobs = new Jobs(scopeFactory, _settings.LoggerFactory.SafeCreateLogger<Jobs>());
        }

        [TestCleanup]
        public void TestCleanup()
        {
            _jobs.SafeDispose();
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
        public void TestPeriodicJobCancelling()
        {
            _jobs.StartJobWorkers();

            _jobs.PublishPeriodic<MockJob, bool>(true, "test_job", 1);

            Thread.Sleep(3500);

            _jobs.PublishPeriodic<MockJob, bool>(true, "test_job", 0);

            Thread.Sleep(1000);

            Assert.AreEqual(3, _performCount);
        }

        private class MockJob
        {
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
    }
}
