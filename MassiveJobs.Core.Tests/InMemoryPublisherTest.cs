using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Threading;
using System.Threading.Tasks;

using MassiveJobs.Core.Memory;
using System.Collections.Generic;

namespace MassiveJobs.Core.Tests
{
    [TestClass]
    public class InMemoryPublisherTest
    {
        private readonly MassiveJobsSettings _settings = new MassiveJobsSettings
        {
            ImmediateWorkersCount = 2,
            ScheduledWorkersCount = 2,
            PeriodicWorkersCount = 2
        };

        private InMemoryMessages _messages;
        private Counter _counter;

        private List<TestTimer> _timers;
        private TestTimeProvider _timeProvider;

        [TestInitialize]
        public void TestInit()
        {
            _counter = new Counter();

            _messages = new InMemoryMessages();

            _timers = new List<TestTimer>();
            _timeProvider = new TestTimeProvider();

            JobsBuilder.Configure()
                .RegisterInstance(_settings)
                .RegisterInstance(_counter)
                .RegisterInstance<ITimeProvider>(_timeProvider)
                .RegisterTransient<ITimer>(f =>
                {
                    var timer = new TestTimer();
                    _timers.Add(timer);
                    return timer;
                })
                .WithInMemoryBroker(_messages)
                .Build();
        }

        [TestCleanup]
        public void TestCleanup()
        {
            var task = Task.Factory.StartNew(JobsBuilder.DisposeJobs);

            while (!task.IsCompleted)
            {
                FireActiveTimers();
            }
        }

        [TestMethod]
        public void TestPublishInc()
        {
            MockJobInc.Publish(true);

            Thread.Sleep(100);

            Assert.AreEqual(1, _counter.Value);
        }

        [TestMethod]
        public void TestPublishWithDelay()
        {
            MockJobInc.Publish(true, TimeSpan.FromSeconds(2));
            MockJobInc.Publish(true, TimeSpan.FromSeconds(2));

            _timeProvider.CurrentTimeUtc = DateTime.UtcNow;
            FireActiveTimers();
            Thread.Sleep(100);

            Assert.AreEqual(0, _counter.Value);

            _timeProvider.AdvanceTime(1000);
            FireActiveTimers();
            Thread.Sleep(100);

            Assert.AreEqual(0, _counter.Value);

            _timeProvider.AdvanceTime(1000);
            FireActiveTimers();
            Thread.Sleep(100);

            Assert.AreEqual(2, _counter.Value);
        }

        [TestMethod]
        public void TestPublishDec()
        {
            MockJobInc.Publish(false);

            Thread.Sleep(100);

            Assert.AreEqual(-1, _counter.Value);
        }

        [TestMethod]
        public void TestFailedJobs()
        {
            MockJobInc.Publish(true);
            MockJobFailed.Publish(true);

            Thread.Sleep(100);

            Assert.AreEqual(1, _counter.Value);
            Assert.AreEqual(1, _messages.GetCount(_settings.ErrorQueueName));
        }

        [TestMethod]
        public void TestFailedAsyncJobs()
        {
            MockJobInc.Publish(true);
            MockAsyncJobFailed.Publish();

            Thread.Sleep(200);

            Assert.AreEqual(1, _counter.Value);
            Assert.AreEqual(1, _messages.GetCount(_settings.ErrorQueueName));
        }

        [TestMethod]
        public void TestCanceledAsyncJobs()
        {
            MockJobInc.Publish(true);
            MockAsyncJobCanceled.Publish();

            Thread.Sleep(200);

            Assert.AreEqual(1, _counter.Value);
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

            Assert.AreEqual(10000, _counter.Value);
        }

        [TestMethod]
        public void TestScheduleWithTimeoutDefault()
        {
            LongRunningJobAsync.Publish(6000);

            Thread.Sleep(6000);

            Assert.AreEqual(0, _counter.Value);
            Assert.AreEqual(1, _messages.GetCount(_settings.ErrorQueueName));
        }

        [TestMethod]
        public void TestScheduleWithTimeoutCustom()
        {
            LongRunningJobAsync.Publish(2000, 1000);

            Thread.Sleep(2000);

            Assert.AreEqual(0, _counter.Value);
            Assert.AreEqual(1, _messages.GetCount(_settings.ErrorQueueName));
        }
        
        [TestMethod]
        public void TestScheduleWithCancelledJobs()
        {
            LongRunningJobAsync.Publish(2000);

            TestCleanup();
            //MassiveJobsMediator.DefaultInstance.StopJobWorkers();

            Assert.AreEqual(0, _counter.Value);

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
            var startTime = new DateTime(2021, 9, 5, 0, 0, 0, DateTimeKind.Utc);
            MockJob.PublishPeriodic("test_job", 1, startTime);

            _timeProvider.CurrentTimeUtc = startTime;
            FireActiveTimers();
            Thread.Sleep(100);

            Assert.AreEqual(0, _counter.Value);

            _timeProvider.CurrentTimeUtc = startTime.AddMilliseconds(1000);
            FireActiveTimers();
            Thread.Sleep(100);

            Assert.AreEqual(1, _counter.Value);

            _timeProvider.CurrentTimeUtc = startTime.AddMilliseconds(1999);
            FireActiveTimers();
            Thread.Sleep(100);

            Assert.AreEqual(1, _counter.Value);

            _timeProvider.CurrentTimeUtc = startTime.AddMilliseconds(2000);
            FireActiveTimers();
            Thread.Sleep(100);

            Assert.AreEqual(2, _counter.Value);

            _timeProvider.CurrentTimeUtc = startTime.AddMilliseconds(2999);
            FireActiveTimers();
            Thread.Sleep(100);

            Assert.AreEqual(2, _counter.Value);

            _timeProvider.CurrentTimeUtc = startTime.AddMilliseconds(3000);
            FireActiveTimers();
            Thread.Sleep(100);

            Assert.AreEqual(3, _counter.Value);
        }

        [TestMethod]
        public void TestPeriodicJobWithEndTime()
        {
            var startTime = new DateTime(2021, 9, 5, 0, 0, 0, DateTimeKind.Utc);
            MockJob.PublishPeriodic("test_job", 1, startTime, startTime.AddMilliseconds(4000));

            _timeProvider.CurrentTimeUtc = startTime;
            FireActiveTimers();
            Thread.Sleep(100);

            Assert.AreEqual(0, _counter.Value);

            _timeProvider.CurrentTimeUtc = startTime.AddMilliseconds(1000);
            FireActiveTimers();
            Thread.Sleep(100);

            Assert.AreEqual(1, _counter.Value);

            _timeProvider.CurrentTimeUtc = startTime.AddMilliseconds(2000);
            FireActiveTimers();
            Thread.Sleep(100);

            Assert.AreEqual(2, _counter.Value);

            _timeProvider.CurrentTimeUtc = startTime.AddMilliseconds(3000);
            FireActiveTimers();
            Thread.Sleep(100);

            Assert.AreEqual(3, _counter.Value);

            _timeProvider.CurrentTimeUtc = startTime.AddMilliseconds(4000);
            FireActiveTimers();
            Thread.Sleep(100);

            Assert.AreEqual(4, _counter.Value);

            _timeProvider.CurrentTimeUtc = startTime.AddMilliseconds(5000);
            FireActiveTimers();
            Thread.Sleep(100);

            Assert.AreEqual(4, _counter.Value);
        }

        [TestMethod]
        public void TestCronJobWithEndTime()
        {
            var startTime = new DateTime(2021, 9, 5, 0, 0, 0, DateTimeKind.Utc);
            MockJob.PublishPeriodic("test_job", "0/2 * * ? * *", null, startTime, startTime.AddSeconds(4));

            _timeProvider.CurrentTimeUtc = startTime;
            FireActiveTimers();
            Thread.Sleep(2000);

            Assert.AreEqual(0, _counter.Value);

            _timeProvider.CurrentTimeUtc = startTime.AddMilliseconds(1000);
            FireActiveTimers();
            Thread.Sleep(100);

            Assert.AreEqual(0, _counter.Value);

            _timeProvider.CurrentTimeUtc = startTime.AddMilliseconds(2000);
            FireActiveTimers();
            Thread.Sleep(100);

            Assert.AreEqual(1, _counter.Value);

            _timeProvider.CurrentTimeUtc = startTime.AddMilliseconds(3000);
            FireActiveTimers();
            Thread.Sleep(100);

            Assert.AreEqual(1, _counter.Value);

            _timeProvider.CurrentTimeUtc = startTime.AddMilliseconds(4000);
            FireActiveTimers();
            Thread.Sleep(100);

            Assert.AreEqual(2, _counter.Value);

            _timeProvider.CurrentTimeUtc = startTime.AddMilliseconds(5000);
            FireActiveTimers();
            Thread.Sleep(100);

            Assert.AreEqual(2, _counter.Value);
        }

        [TestMethod]
        public void TestPeriodicJobCancelling()
        {
            var startTime = new DateTime(2021, 9, 5, 0, 0, 0, DateTimeKind.Utc);
            MockJob.PublishPeriodic("test_job", 1, startTime);

            _timeProvider.CurrentTimeUtc = startTime;
            FireActiveTimers();
            Thread.Sleep(100);

            Assert.AreEqual(0, _counter.Value);

            _timeProvider.CurrentTimeUtc = startTime.AddMilliseconds(1000);
            FireActiveTimers();
            Thread.Sleep(100);

            Assert.AreEqual(1, _counter.Value);

            MockJobInc.CancelPeriodic("test_job");
            FireActiveTimers();
            Thread.Sleep(100);

            _timeProvider.CurrentTimeUtc = startTime.AddMilliseconds(2000);
            FireActiveTimers();
            Thread.Sleep(100);

            Assert.AreEqual(1, _counter.Value);
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

        private void FireActiveTimers()
        {
            foreach (var t in _timers) t.FireTimeElapsedIfActive();
        }

        private class Counter
        {
            public int Value;
        }

        private class MockJob: Job<MockJob>
        {
            private readonly Counter _counter;

            public MockJob(Counter counter)
            {
                _counter = counter;
            }

            public override void Perform()
            {
                Interlocked.Increment(ref _counter.Value);
            }
        }

        private class MockJobInc: Job<MockJobInc, bool>
        {
            private readonly Counter _counter;

            public MockJobInc(Counter counter)
            {
                _counter = counter;
            }

            public override void Perform(bool increment)
            {
                if (increment) Interlocked.Increment(ref _counter.Value);
                else Interlocked.Decrement(ref _counter.Value);
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
            private readonly Counter _counter;

            public LongRunningJobAsync(Counter counter)
            {
                _counter = counter;
            }

            public override async Task Perform(int delayMs, CancellationToken cancellationToken)
            {
                await Task.Delay(delayMs, cancellationToken);
                Interlocked.Increment(ref _counter.Value);
            }
        }
    }
}
