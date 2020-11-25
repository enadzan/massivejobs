using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

using MassiveJobs.Core;
using MassiveJobs.Core.Serialization;

namespace MassiveJobs.RabbitMqBroker.Tests
{
    [TestClass]
    public class RabbitMqPublisherTest
    {
        private static int _performCount;

        [TestInitialize]
        public void TestInit()
        {
            _performCount = 0;

            RabbitMqJobs.Initialize(true, s =>
            {
                s.RabbitMqSettings.VirtualHost = "massivejobs.tests";
                s.RabbitMqSettings.NamePrefix = "tests.";
                s.RabbitMqSettings.PrefetchCount = 400;
                s.PublishBatchSize = 200;
                s.ImmediateWorkersBatchSize = 400;

                s.MaxDegreeOfParallelismPerWorker = 2;
                s.ImmediateWorkersCount = 3;
                s.ScheduledWorkersCount = 2;
                s.PeriodicWorkersCount = 2;

                s.JobLoggerFactory = new DebugLoggerFactory();
                s.JobSerializer = new SimpleBinarySerializer();
                s.JobTypeProvider = new TypeProvider();
            });
        }

        [TestCleanup]
        public void TestCleanup()
        {
            MassiveJobsMediator.DefaultInstance.SafeDispose();
        }

        [TestMethod]
        public void Publish_should_not_throw_exception()
        {
            // warm up (establish connection)
            DummyJobWithArgs2.Publish(new DummyJobArgs2());

            using (var tokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(10)))
            {
                while (_performCount < 2)
                {
                    Task.Delay(100, tokenSource.Token).Wait(tokenSource.Token);
                }
            }

            var watch = Stopwatch.StartNew();

            JobBatch.Do(() =>
            {
                for (var i = 0; i < 100_000; i++)
                {
                    DummyJobWithArgs.Publish(new DummyJobArgs { SomeId = i });
                }
            });

            watch.Stop();

            Console.WriteLine($"Elapsed: {watch.Elapsed}");
            
            using (var tokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(10)))
            {
                while (_performCount < 100_002)
                {
                    Task.Delay(100, tokenSource.Token).Wait(tokenSource.Token);
                }
            }

            Assert.AreEqual(100_002, _performCount);
        }

        [TestMethod]
        public void Publish_long_running_should_not_throw_exception()
        {
            DummyJobWithArgs.Publish(new DummyJobArgs { SomeId = 1 }, 10_000); // 10 sec timeout
            
            using (var tokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(10)))
            {
                while (_performCount < 1)
                {
                    Task.Delay(100, tokenSource.Token).Wait(tokenSource.Token);
                }
            }

            Assert.AreEqual(1, _performCount);
        }

        [TestMethod]
        public void PublishPeriodic_should_run_jobs_periodically_until_end()
        {
            var endAtUtc = DateTime.UtcNow.AddSeconds(4.5);

            DummyJob.PublishPeriodic("test_periodic", 1, null, endAtUtc);
            DummyJob.PublishPeriodic("test_periodic", 1, null, endAtUtc);
            DummyJob.PublishPeriodic("test_periodic", 1, null, endAtUtc);

            Thread.Sleep(6000);

            Assert.AreEqual(4, _performCount);
        }

        [TestMethod]
        public void CronJob_should_run_jobs_periodically_until_end()
        {
            while (DateTime.UtcNow.Second % 2 != 0)
            {
                Thread.Sleep(100);
            }

            var endAtUtc = DateTime.UtcNow.AddSeconds(4.1);

            DummyJob.PublishPeriodic("test_periodic", "0/2 * * ? * *", null, null, endAtUtc);

            // these should be ignored
            DummyJob.PublishPeriodic("test_periodic", 1, null, endAtUtc);
            DummyJob.PublishPeriodic("test_periodic", 1, null, endAtUtc);

            Thread.Sleep(6000);

            Assert.AreEqual(2, _performCount);
        }

        private class DummyJob: Job<DummyJob>
        {
            public override void Perform()
            {
                Interlocked.Increment(ref _performCount);
            }
        }

        private class DummyJobWithArgs : Job<DummyJobWithArgs, DummyJobArgs>
        {
            public override void Perform(DummyJobArgs _)
            {
                Interlocked.Increment(ref _performCount);
            }
        }

        private class DummyJobWithArgs2 : Job<DummyJobWithArgs2, DummyJobArgs2>
        {
            public override void Perform(DummyJobArgs2 _)
            {
                Interlocked.Increment(ref _performCount);
                Interlocked.Increment(ref _performCount);
            }
        }

        private class DummyJobArgs
        {
            [PropertyOrder(0)]
            public int SomeId { get; set; }

            [PropertyOrder(1)]
            public string SomeName { get; set; }
        }

        private class DummyJobArgs2
        {
            [PropertyOrder(0)]
            public int SomeId2 { get; set; }

            [PropertyOrder(1)]
            public string SomeName2 { get; set; }
        }

        private class TypeProvider : CustomTypeProvider
        {
            public TypeProvider() : base(new[]
            {
                new KeyValuePair<string, Type>("dja", typeof(DummyJobArgs)),
                new KeyValuePair<string, Type>("dja2", typeof(DummyJobArgs2)),
                new KeyValuePair<string, Type>("dj", typeof(DummyJob)),
                new KeyValuePair<string, Type>("djwa", typeof(DummyJobWithArgs)),
                new KeyValuePair<string, Type>("djwa2", typeof(DummyJobWithArgs2)),
            })
            {
            }
        }
    }
}
