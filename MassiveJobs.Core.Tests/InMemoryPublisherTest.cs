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

        [TestInitialize]
        public void TestInit()
        {
            _performCount = 0;

            _messages = new InMemoryMessages();

            JobsBuilder.Configure()
                .RegisterInstance(_settings)
                .WithInMemoryBroker(_messages)
                .Build();
        }

        [TestCleanup]
        public void TestCleanup()
        {
            JobsBuilder.DisposeJobs();
        }

        [TestMethod]
        public void TestPublishInc()
        {
            MockJobInc.Publish(true);

            Thread.Sleep(100);

            Assert.AreEqual(1, _performCount);
        }

        [TestMethod]
        public void TestPublishWithDelay()
        {
            MockJobInc.Publish(true, TimeSpan.FromSeconds(2));
            MockJobInc.Publish(true, TimeSpan.FromSeconds(2));

            Thread.Sleep(1000);

            Assert.AreEqual(0, _performCount);

            Thread.Sleep(1500);

            Assert.AreEqual(2, _performCount);
        }

        [TestMethod]
        public void TestPublishDec()
        {
            MockJobInc.Publish(false);

            Thread.Sleep(100);

            Assert.AreEqual(-1, _performCount);
        }

        [TestMethod]
        public void TestFailedJobs()
        {
            MockJobInc.Publish(true);
            MockJobFailed.Publish(true);

            Thread.Sleep(100);

            Assert.AreEqual(1, _performCount);
            Assert.AreEqual(1, _messages.GetCount(_settings.ErrorQueueName));
        }

        [TestMethod]
        public void TestFailedAsyncJobs()
        {
            MockJobInc.Publish(true);
            MockAsyncJobFailed.Publish();

            Thread.Sleep(100);

            Assert.AreEqual(1, _performCount);
            Assert.AreEqual(1, _messages.GetCount(_settings.ErrorQueueName));
        }

        [TestMethod]
        public void TestCanceledAsyncJobs()
        {
            MockJobInc.Publish(true);
            MockAsyncJobCanceled.Publish();

            Thread.Sleep(200);

            Assert.AreEqual(1, _performCount);
            Assert.AreEqual(1,  _messages.GetCount(_settings.ErrorQueueName));
        }

        [TestMethod]
        public void TestScheduleParallel()
        {
            for (var i = 0; i < 10000; i++)
            {
                MockJobInc.Publish(true);
            }

            Thread.Sleep(500);

            Assert.AreEqual(10000, _performCount);
        }

        [TestMethod]
        public void TestScheduleWithTimeoutDefault()
        {
            LongRunningJobAsync.Publish(6000);

            Thread.Sleep(6000);

            Assert.AreEqual(0, _performCount);
            Assert.AreEqual(1, _messages.GetCount(_settings.ErrorQueueName));
        }

        [TestMethod]
        public void TestScheduleWithTimeoutCustom()
        {
            LongRunningJobAsync.Publish(2000, 1000);

            Thread.Sleep(2000);

            Assert.AreEqual(0, _performCount);
            Assert.AreEqual(1, _messages.GetCount(_settings.ErrorQueueName));
        }
        
        [TestMethod]
        public void TestScheduleWithCancelledJobs()
        {
            LongRunningJobAsync.Publish(2000);

            MassiveJobsMediator.DefaultInstance.StopJobWorkers();

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
            MockJobInc.PublishPeriodic(true, "test_job", 1);

            Thread.Sleep(3500);

            Assert.AreEqual(3, _performCount);
        }

        [TestMethod]
        public void TestPeriodicJobWithEndTime()
        {
            MockJobInc.PublishPeriodic(true, "test_job", 1, null, DateTime.UtcNow.AddMilliseconds(4500));

            Thread.Sleep(6000);

            Assert.AreEqual(4, _performCount);
        }

        [TestMethod]
        public void TestCronJobWithEndTime()
        {
            MockJob.PublishPeriodic("test_job", "0/2 * * ? * *", null, null, DateTime.UtcNow.AddSeconds(4));

            Thread.Sleep(6000);

            Assert.IsTrue(_performCount == 2);
        }

        [TestMethod]
        public void TestPeriodicJobCancelling()
        {
            MockJobInc.PublishPeriodic(true, "test_job", 1);

            Thread.Sleep(3500);

            MockJobInc.CancelPeriodic("test_job");

            Thread.Sleep(1000);

            Assert.AreEqual(3, _performCount);
        }

        [TestMethod]
        public void TestLongRunningImmediateJob()
        {
            MockJobInc.Publish(true, 10_000);

            var totalLongRunning = 0;
            for (var i = 0; i < _settings.LongRunningWorkersCount; i++)
            {
                totalLongRunning +=
                    _messages.GetCount(string.Format(_settings.LongRunningQueueNameTemplate, i));
            }

            Assert.AreEqual(1, totalLongRunning);
        }

        private class MockJob: Job<MockJob>
        {
            public override void Perform()
            {
                Interlocked.Increment(ref _performCount);
            }
        }

        private class MockJobInc: Job<MockJobInc, bool>
        {
            public override void Perform(bool increment)
            {
                if (increment) Interlocked.Increment(ref _performCount);
                else Interlocked.Decrement(ref _performCount);
            }
        }

        private class MockJobFailed: Job<MockJobFailed, bool>
        {
            public static bool ShoudFail { get; set; } = true;

            public override void Perform(bool _)
            {
                if (ShoudFail)
                {
                    ShoudFail = false;
                    throw new Exception("Testing error in async job");
                }
            }
        }

        private class MockAsyncJobFailed: JobAsync<MockAsyncJobFailed>
        {
            public override async Task Perform(CancellationToken cancellationToken)
            {
                await Task.Delay(10, cancellationToken);

                throw new Exception("Testing error in async job");
            }
        }

        private class MockAsyncJobCanceled: JobAsync<MockAsyncJobCanceled>
        {
            public override async Task Perform(CancellationToken cancellationToken)
            {
                await Task.Delay(100, cancellationToken);
                throw new OperationCanceledException();
            }
        }

        private class LongRunningJobAsync: JobAsync<LongRunningJobAsync, int>
        {
            public override async Task Perform(int delayMs, CancellationToken cancellationToken)
            {
                await Task.Delay(delayMs, cancellationToken);
                Interlocked.Increment(ref _performCount);
            }
        }
    }
}
