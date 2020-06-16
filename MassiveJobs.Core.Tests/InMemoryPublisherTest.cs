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

        [TestInitialize]
        public void TestInit()
        {
            _performCount = 0;
        }

        [TestMethod]
        public void TestPublishInc()
        {
            using var publisher = new InMemoryPublisher();

            publisher.StartJobWorkers();

            publisher.Publish<MockJob, bool>(true);

            publisher.WaitCompleted();

            Assert.AreEqual(1, _performCount);
        }

        [TestMethod]
        public void TestPublishDec()
        {
            using var publisher = new InMemoryPublisher();

            publisher.StartJobWorkers();

            publisher.Publish<MockJob, bool>(false);

            publisher.WaitCompleted();

            Assert.AreEqual(-1, _performCount);
        }

        [TestMethod]
        public void TestFailedJobs()
        {
            using var publisher = new InMemoryPublisher();

            publisher.StartJobWorkers();

            publisher.Publish<MockJob, bool>(true);
            publisher.Publish<MockJobFailed, bool>(true);

            publisher.WaitCompleted();

            Assert.AreEqual(1, _performCount);
            Assert.AreEqual(1, publisher.JobsCount);
        }

        [TestMethod]
        public void TestFailedAsyncJobs()
        {
            using var publisher = new InMemoryPublisher();

            publisher.StartJobWorkers();

            publisher.Publish<MockJob, bool>(true);
            publisher.Publish<MockAsyncJobFailed, bool>(true);

            publisher.WaitCompleted();

            Assert.AreEqual(1, _performCount);
            Assert.AreEqual(1, publisher.JobsCount);
        }

        [TestMethod]
        public void TestCanceledAsyncJobs()
        {
            using var publisher = new InMemoryPublisher();

            publisher.StartJobWorkers();

            publisher.Publish<MockJob, bool>(true);
            publisher.Publish<MockAsyncJobCanceled, bool>(true);

            publisher.WaitCompleted();

            Assert.AreEqual(1, _performCount);
            Assert.AreEqual(1, publisher.JobsCount);
        }

        [TestMethod]
        public void TestScheduleParallel()
        {
            using var publisher = new InMemoryPublisher();

            publisher.StartJobWorkers();

            for (var i = 0; i < 10000; i++)
            {
                publisher.Publish<MockJob, bool>(true);
            }

            publisher.WaitCompleted();

            Assert.AreEqual(10000, _performCount);
        }

        [TestMethod]
        public void TestScheduleWithTimeoutDefault()
        {
            using var publisher = new InMemoryPublisher();

            publisher.StartJobWorkers();

            publisher.Publish<LongRunningJobAsync, int>(6000);

            publisher.WaitCompleted();

            Assert.AreEqual(0, _performCount);

            publisher.StopJobWorkers();

            var topJob = publisher.JobsTop;
            Assert.IsNotNull(topJob);
            Assert.IsTrue(topJob.Err?.StartsWith("A job timed out.") ?? false);
        }

        [TestMethod]
        public void TestScheduleWithTimeoutCustom()
        {
            using var publisher = new InMemoryPublisher();

            publisher.StartJobWorkers();

            publisher.Publish<LongRunningJobAsync, int>(2000, null, 1000);

            publisher.WaitCompleted();

            Assert.AreEqual(0, _performCount);

            publisher.StopJobWorkers();

            var topJob = publisher.JobsTop;
            Assert.IsNotNull(topJob);
            Assert.IsTrue(topJob.Err?.StartsWith("A job timed out.") ?? false);
        }
        
        [TestMethod]
        public void TestScheduleWithCancelledJobs()
        {
            using var publisher = new InMemoryPublisher();

            publisher.StartJobWorkers();

            publisher.Publish<LongRunningJobAsync, int>(2000);

            publisher.StopJobWorkers();

            Assert.AreEqual(0, _performCount);

            var topJob = publisher.JobsTop;
            Assert.IsNotNull(topJob);
            Assert.IsTrue(topJob.Err?.StartsWith("A task was canceled.") ?? false);
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
            public void Perform(bool _)
            {
                throw new Exception("Testing error in async job");
            }
        }

        private class MockAsyncJob
        {
            public async Task Perform(bool increment, CancellationToken cancellationToken)
            {
                await Task.Delay(1000, cancellationToken);

                if (increment) Interlocked.Increment(ref _performCount);
                else Interlocked.Decrement(ref _performCount);
            }
        }

        private class MockAsyncJobFailed
        {
            public async Task Perform(bool _, CancellationToken cancellationToken)
            {
                await Task.Delay(1000, cancellationToken);

                throw new Exception("Testing error in async job");
            }
        }

        private class MockAsyncJobCanceled
        {
            public async Task Perform(bool _, CancellationToken cancellationToken)
            {
                await Task.Delay(1000, cancellationToken);
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
