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
        private readonly MassiveJobsSettings _settings = new MassiveJobsSettings
        {
            ImmediateWorkersCount = 2,
            ScheduledWorkersCount = 2,
            PeriodicWorkersCount = 2
        };

        private InMemoryMessages _messages;
        private Counter _counter;

        [TestInitialize]
        public void TestInit()
        {
            _counter = new Counter();

            _messages = new InMemoryMessages();

            JobsBuilder.Configure()
                .RegisterInstance(_settings)
                .RegisterInstance(_counter)
                .WithInMemoryBroker(_messages)
                .Build();
        }

        [TestCleanup]
        public void TestCleanup()
        {
            JobsBuilder.DisposeJobs();
            _messages.Dispose();
        }

        private void CancelWorkers()
        {
            MassiveJobsMediator.DefaultInstance.CancelJobWorkers();
        }

        [TestMethod]
        public void TestPublishInc()
        {
            MockJobInc.Publish(true);

            Thread.Sleep(250);

            Assert.AreEqual(1, _counter.Value);
        }

        [TestMethod]
        public void TestPublishWithDelay()
        {
            MockJobInc.Publish(true, TimeSpan.FromSeconds(2));
            MockJobInc.Publish(true, TimeSpan.FromSeconds(2));

            Thread.Sleep(1000);
            Assert.AreEqual(0, _counter.Value);

            Thread.Sleep(1250);
            Assert.AreEqual(2, _counter.Value);
        }

        [TestMethod]
        public void TestPublishDec()
        {
            MockJobInc.Publish(false);

            Thread.Sleep(250);

            Assert.AreEqual(-1, _counter.Value);
        }

        [TestMethod]
        public void TestFailedJobs()
        {
            MockJobInc.Publish(true);
            MockJobFailed.Publish(true);

            Thread.Sleep(250);

            Assert.AreEqual(1, _counter.Value);
            Assert.AreEqual(1, _messages.GetCount());
        }

        [TestMethod]
        public void TestFailedAsyncJobs()
        {
            MockJobInc.Publish(true);
            MockAsyncJobFailed.Publish();

            Thread.Sleep(250);

            Assert.AreEqual(1, _counter.Value);
            Assert.AreEqual(1, _messages.GetCount());
        }

        [TestMethod]
        public void TestCanceledAsyncJobs()
        {
            MockJobInc.Publish(true);
            MockAsyncJobCanceled.Publish();

            Thread.Sleep(250);

            Assert.AreEqual(1, _counter.Value);
            Assert.AreEqual(1,  _messages.GetCount());
        }

        [TestMethod]
        public void TestPublishParallel()
        {
            Parallel.For(0, 100000, _ => MockJob.Publish());

            Thread.Sleep(1000);

            Assert.AreEqual(100000, _counter.Value);
        }

        [TestMethod]
        public void TestPublishInBatch()
        {
            JobBatch.Do(() =>
            {
                for (var i = 0; i < 100000; i++)
                {
                    MockJob.Publish();
                }
            });

            Thread.Sleep(1000);

            Assert.AreEqual(100000, _counter.Value);
        }

        [TestMethod]
        public void TestPublishWithTimeoutDefault()
        {
            LongRunningJobAsync.Publish(6000);

            Thread.Sleep(7000);

            Assert.AreEqual(0, _counter.Value);
            Assert.AreEqual(1, _messages.GetCount());
        }

        [TestMethod]
        public void TestPublishWithTimeoutCustom()
        {
            LongRunningJobAsync.Publish(2000, 1000);

            Thread.Sleep(3000);

            Assert.AreEqual(0, _counter.Value);
            Assert.AreEqual(1, _messages.GetCount());
        }
        
        [TestMethod]
        public void TestPublishWithCancelledJobs()
        {
            LongRunningJobAsync.Publish(2000);

            CancelWorkers();

            Assert.AreEqual(0, _counter.Value);
            Assert.AreEqual(1, _messages.GetCount());
        }

        [TestMethod]
        public void TestPeriodicJob()
        {
            MockJob.PublishPeriodic("test_job", 1);

            Thread.Sleep(250);
            Assert.AreEqual(0, _counter.Value);

            Thread.Sleep(1000);
            Assert.AreEqual(1, _counter.Value);

            Thread.Sleep(1000);
            Assert.AreEqual(2, _counter.Value);

            Thread.Sleep(1000);
            Assert.AreEqual(3, _counter.Value);
        }

        [TestMethod]
        public void TestPeriodicJobWithEndTime()
        {
            MockJob.PublishPeriodic("test_job", 1, null, DateTime.UtcNow.AddMilliseconds(4250));

            Thread.Sleep(250);
            Assert.AreEqual(0, _counter.Value);

            Thread.Sleep(1000);
            Assert.AreEqual(1, _counter.Value);

            Thread.Sleep(1000);
            Assert.AreEqual(2, _counter.Value);

            Thread.Sleep(1000);
            Assert.AreEqual(3, _counter.Value);

            Thread.Sleep(1000);
            Assert.AreEqual(4, _counter.Value);

            Thread.Sleep(1000);
            Assert.AreEqual(4, _counter.Value);
        }

        [TestMethod]
        public void TestCronJobWithEndTime()
        {
            // round to seconds
            var startTime = new DateTime(DateTime.UtcNow.Ticks / TimeSpan.TicksPerMillisecond / 1000 * 1000 * TimeSpan.TicksPerMillisecond, DateTimeKind.Utc);

            startTime = startTime.AddSeconds(startTime.Second % 2 == 1 ? 1 : 2);
            var endTime = startTime.AddMilliseconds(4250);

            MockJob.PublishPeriodic("test_job", "0/2 * * ? * *", null, startTime, endTime);

            //Console.WriteLine("Start Time: " + startTime);
            //Console.WriteLine("End Time: " + endTime);

            Thread.Sleep(7250);

            Assert.AreEqual(3, _counter.Value);
        }

        [TestMethod]
        public void TestPeriodicJobCancelling()
        {
            MockJob.PublishPeriodic("test_job", 1);

            Thread.Sleep(250);
            Assert.AreEqual(0, _counter.Value);

            Thread.Sleep(1000);
            Assert.AreEqual(1, _counter.Value);

            MockJobInc.CancelPeriodic("test_job");

            Thread.Sleep(1000);
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

        private class Counter
        {
            public int Value;
        }

        // ReSharper disable ClassNeverInstantiated.Local
        private class MockJob: Job<MockJob>
        {
            private readonly Counter _counter;

            public MockJob(Counter counter)
            {
                _counter = counter;
            }

            public override void Perform()
            {
                //Console.WriteLine(DateTime.UtcNow);
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
            public static bool ShouldFail { get; set; } = true;

            public override void Perform(bool _)
            {
                if (!ShouldFail) return;

                ShouldFail = false;
                throw new Exception("Testing error in async job");
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
        // ReSharper restore ClassNeverInstantiated.Local
    }
}
