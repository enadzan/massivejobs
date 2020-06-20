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
            using var publisher = InMemoryPublisherBuilder.CreateBuilder().Build();

            publisher.StartJobWorkers();

            publisher.Publish<MockJob, bool>(true);

            Thread.Sleep(50);

            Assert.AreEqual(1, _performCount);
        }

        [TestMethod]
        public void TestPublishDec()
        {
            using var publisher = InMemoryPublisherBuilder.CreateBuilder().Build();

            publisher.StartJobWorkers();

            publisher.Publish<MockJob, bool>(false);

            Thread.Sleep(50);

            Assert.AreEqual(-1, _performCount);
        }

        [TestMethod]
        public void TestFailedJobs()
        {
            InMemoryMessages messages = null;
            string errorQueueName = null;

            using var publisher = InMemoryPublisherBuilder.CreateBuilder()
                .Configure(s =>
                {
                    messages = ((InMemoryMessageBrokerFactory)s.MessageBrokerFactory).Messages;
                    errorQueueName = s.ErrorQueueName;
                })
                .Build();

            publisher.StartJobWorkers();

            publisher.Publish<MockJob, bool>(true);
            publisher.Publish<MockJobFailed, bool>(true);

            Thread.Sleep(50);

            Assert.AreEqual(1, _performCount);
            Assert.AreEqual(1, messages.GetCount(errorQueueName));
        }

        [TestMethod]
        public void TestFailedAsyncJobs()
        {
            InMemoryMessages messages = null;
            string errorQueueName = null;

            using var publisher = InMemoryPublisherBuilder.CreateBuilder()
                .Configure(s =>
                {
                    messages = ((InMemoryMessageBrokerFactory)s.MessageBrokerFactory).Messages;
                    errorQueueName = s.ErrorQueueName;
                })
                .Build();

            publisher.StartJobWorkers();

            publisher.Publish<MockJob, bool>(true);
            publisher.Publish<MockAsyncJobFailed, bool>(true);

            Thread.Sleep(100);

            Assert.AreEqual(1, _performCount);
            Assert.AreEqual(1, messages.GetCount(errorQueueName));
        }

        [TestMethod]
        public void TestCanceledAsyncJobs()
        {
            InMemoryMessages messages = null;
            string errorQueueName = null;

            using var publisher = InMemoryPublisherBuilder.CreateBuilder()
                .Configure(s =>
                {
                    messages = ((InMemoryMessageBrokerFactory)s.MessageBrokerFactory).Messages;
                    errorQueueName = s.ErrorQueueName;
                })
                .Build();

            publisher.StartJobWorkers();

            publisher.Publish<MockJob, bool>(true);
            publisher.Publish<MockAsyncJobCanceled, bool>(true);

            Thread.Sleep(200);

            Assert.AreEqual(1, _performCount);
            Assert.AreEqual(1,  messages.GetCount(errorQueueName));
        }

        [TestMethod]
        public void TestScheduleParallel()
        {
            using var publisher = InMemoryPublisherBuilder.CreateBuilder().Build();

            publisher.StartJobWorkers();

            for (var i = 0; i < 10000; i++)
            {
                publisher.Publish<MockJob, bool>(true);
            }

            Thread.Sleep(500);

            Assert.AreEqual(10000, _performCount);
        }

        [TestMethod]
        public void TestScheduleWithTimeoutDefault()
        {
            InMemoryMessages messages = null;
            string errorQueueName = null;

            using var publisher = InMemoryPublisherBuilder.CreateBuilder()
                .Configure(s =>
                {
                    messages = ((InMemoryMessageBrokerFactory)s.MessageBrokerFactory).Messages;
                    errorQueueName = s.ErrorQueueName;
                })
                .Build();

            publisher.StartJobWorkers();

            publisher.Publish<LongRunningJobAsync, int>(6000);

            Thread.Sleep(6000);

            Assert.AreEqual(0, _performCount);
            Assert.AreEqual(1, messages.GetCount(errorQueueName));
        }

        [TestMethod]
        public void TestScheduleWithTimeoutCustom()
        {
            InMemoryMessages messages = null;
            string errorQueueName = null;

            using var publisher = InMemoryPublisherBuilder.CreateBuilder()
                .Configure(s =>
                {
                    messages = ((InMemoryMessageBrokerFactory)s.MessageBrokerFactory).Messages;
                    errorQueueName = s.ErrorQueueName;
                })
                .Build();

            publisher.StartJobWorkers();

            publisher.Publish<LongRunningJobAsync, int>(2000, null, 1000);

            Thread.Sleep(2000);

            Assert.AreEqual(0, _performCount);
            Assert.AreEqual(1, messages.GetCount(errorQueueName));
        }
        
        [TestMethod]
        public void TestScheduleWithCancelledJobs()
        {
            InMemoryMessages messages = null;
            string errorQueueName = null;

            using var publisher = InMemoryPublisherBuilder.CreateBuilder()
                .Configure(s =>
                {
                    messages = ((InMemoryMessageBrokerFactory)s.MessageBrokerFactory).Messages;
                    errorQueueName = s.ErrorQueueName;
                })
                .Build();

            publisher.StartJobWorkers();

            publisher.Publish<LongRunningJobAsync, int>(2000);

            publisher.StopJobWorkers();

            Assert.AreEqual(0, _performCount);
            Assert.AreEqual(1, messages.GetCount(errorQueueName));
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
                await Task.Delay(10, cancellationToken);

                throw new Exception("Testing error in async job");
            }
        }

        private class MockAsyncJobCanceled
        {
            public async Task Perform(bool _, CancellationToken cancellationToken)
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
