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
        private IServiceScope _scope;
        private DefaultServiceScopeFactory _scopeFactory;

        private IWorkerCoordinator _workerCoordinator;
        private IJobPublisher _jobPublisher;

        [TestInitialize]
        public void TestInit()
        {
            _performCount = 0;

            _messages = new InMemoryMessages();

            var messagePublisher = new InMemoryMessagePublisher(_settings, _messages);
            var messageConsumer = new InMemoryMessageConsumer(_messages);

            _scopeFactory = new DefaultServiceScopeFactory(_settings);

            _scopeFactory.ServiceCollection.AddSingleton<IMessagePublisher>(messagePublisher);
            _scopeFactory.ServiceCollection.AddSingleton<IMessageConsumer>(messageConsumer);
            _scopeFactory.ServiceCollection.AddSingleton<IWorkerCoordinator>(
                new WorkerCoordinator(_settings, messageConsumer, _scopeFactory, _settings.LoggerFactory)
            );

            _scope = _scopeFactory.CreateScope();

            _workerCoordinator = _scope.GetService<IWorkerCoordinator>();
            _jobPublisher = _scope.GetService<IJobPublisher>();
        }

        [TestCleanup]
        public void TestCleanup()
        {
            _scope.SafeDispose();
            _scopeFactory.SafeDispose();
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
