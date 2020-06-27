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
            ImmediateWorkersCount = 1,
            ScheduledWorkersCount = 1,
            PeriodicWorkersCount = 1
        };

        private InMemoryMessages _messages;
        private InMemoryMessagePublisher _messagePublisher;
        private InMemoryMessageConsumer _messageConsumer;

        private IServiceScope _serviceScope;
        private IJobPublisher _jobPublisher;
        private IWorkerCoordinator _workerCoordinator;

        [TestInitialize]
        public void TestInit()
        {
            _performCount = 0;

            _messages = new InMemoryMessages();
            _messagePublisher = new InMemoryMessagePublisher(_settings, _messages);
            _messageConsumer = new InMemoryMessageConsumer(_messages);

            _serviceScope = new DefaultServiceScope(_settings, _messagePublisher, _messageConsumer);

            _jobPublisher = _serviceScope.GetService<IJobPublisher>();
            _workerCoordinator = _serviceScope.GetService<IWorkerCoordinator>();
        }

        [TestCleanup]
        public void TestCleanup()
        {
            _workerCoordinator.SafeDispose();
            _jobPublisher.SafeDispose();

            _serviceScope.SafeDispose();

            _messageConsumer.SafeDispose();
            _messagePublisher.SafeDispose();
        }

        [TestMethod]
        public void TestPublishInc()
        {
            _workerCoordinator.StartJobWorkers();

            _jobPublisher.Publish<MockJob, bool>(true);

            Thread.Sleep(100);

            Assert.AreEqual(1, _performCount);
        }

        [TestMethod]
        public void TestPublishDec()
        {
            _workerCoordinator.StartJobWorkers();

            _jobPublisher.Publish<MockJob, bool>(false);

            Thread.Sleep(100);

            Assert.AreEqual(-1, _performCount);
        }

        [TestMethod]
        public void TestFailedJobs()
        {
            _workerCoordinator.StartJobWorkers();

            _jobPublisher.Publish<MockJob, bool>(true);
            _jobPublisher.Publish<MockJobFailed, bool>(true);

            Thread.Sleep(100);

            Assert.AreEqual(1, _performCount);
            Assert.AreEqual(1, _messages.GetCount(_settings.ErrorQueueName));
        }

        [TestMethod]
        public void TestFailedAsyncJobs()
        {
            _workerCoordinator.StartJobWorkers();

            _jobPublisher.Publish<MockJob, bool>(true);
            _jobPublisher.Publish<MockAsyncJobFailed>();

            Thread.Sleep(100);

            Assert.AreEqual(1, _performCount);
            Assert.AreEqual(1, _messages.GetCount(_settings.ErrorQueueName));
        }

        [TestMethod]
        public void TestCanceledAsyncJobs()
        {
            _workerCoordinator.StartJobWorkers();

            _jobPublisher.Publish<MockJob, bool>(true);
            _jobPublisher.Publish<MockAsyncJobCanceled>();

            Thread.Sleep(200);

            Assert.AreEqual(1, _performCount);
            Assert.AreEqual(1,  _messages.GetCount(_settings.ErrorQueueName));
        }

        [TestMethod]
        public void TestScheduleParallel()
        {
            _workerCoordinator.StartJobWorkers();

            for (var i = 0; i < 10000; i++)
            {
                _jobPublisher.Publish<MockJob, bool>(true);
            }

            Thread.Sleep(500);

            Assert.AreEqual(10000, _performCount);
        }

        [TestMethod]
        public void TestScheduleWithTimeoutDefault()
        {
            _workerCoordinator.StartJobWorkers();

            _jobPublisher.Publish<LongRunningJobAsync, int>(6000);

            Thread.Sleep(6000);

            Assert.AreEqual(0, _performCount);
            Assert.AreEqual(1, _messages.GetCount(_settings.ErrorQueueName));
        }

        [TestMethod]
        public void TestScheduleWithTimeoutCustom()
        {
            _workerCoordinator.StartJobWorkers();

            _jobPublisher.Publish<LongRunningJobAsync, int>(2000, null, 1000);

            Thread.Sleep(2000);

            Assert.AreEqual(0, _performCount);
            Assert.AreEqual(1, _messages.GetCount(_settings.ErrorQueueName));
        }
        
        [TestMethod]
        public void TestScheduleWithCancelledJobs()
        {
            _workerCoordinator.StartJobWorkers();

            _jobPublisher.Publish<LongRunningJobAsync, int>(2000);

            _workerCoordinator.StopJobWorkers();

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
            _workerCoordinator.StartJobWorkers();

            _jobPublisher.PublishPeriodic<MockJob, bool>(true, "test_job", 1);

            Thread.Sleep(3500);

            Assert.AreEqual(3, _performCount);
        }

        [TestMethod]
        public void TestPeriodicJobWithEndTime()
        {
            _workerCoordinator.StartJobWorkers();

            System.Diagnostics.Debug.WriteLine(DateTime.Now);

            _jobPublisher.PublishPeriodic<MockJob, bool>(true, "test_job", 1, null, DateTime.UtcNow.AddMilliseconds(4500));

            Thread.Sleep(6000);

            Assert.AreEqual(4, _performCount);
        }

        private class MockJob
        {
            public void Perform(bool increment)
            {
                System.Diagnostics.Debug.WriteLine(DateTime.Now);

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
