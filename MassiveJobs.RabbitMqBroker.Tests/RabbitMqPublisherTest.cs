using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Threading;

using MassiveJobs.Core;
using System.Threading.Tasks;

namespace MassiveJobs.RabbitMqBroker.Tests
{
    [TestClass]
    public class RabbitMqPublisherTest
    {
        private static int _performCount;
        private RabbitMqSettings _settings;

        [TestInitialize]
        public void TestInit()
        {
            _performCount = 0;

            _settings = new RabbitMqSettings
            {
                VirtualHost = "massivejobs.tests",
                NamePrefix = "tests.",
                PrefetchCount = 1000
            };

            RabbitMqJobs.Initialize(true, _settings, s =>
            {
                s.MassiveJobs.PublishBatchSize = 200;
                s.MassiveJobs.ImmediateWorkersCount = 10;
                s.MassiveJobs.ImmediateWorkersBatchSize = 1000;
                s.MassiveJobs.ScheduledWorkersCount = 2;
                s.MassiveJobs.PeriodicWorkersCount = 2;
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
            JobBatch.Do(() =>
            {
                for (var i = 0; i < 100_000; i++)
                {
                    DummyJobWithArgs.Publish(new DummyJobArgs { SomeId = i });
                }
            });
            
            DummyJobWithArgs2.Publish(new DummyJobArgs2());

            using (var tokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(10)))
            {
                while (_performCount < 100_002)
                {
                    Task.Delay(100, tokenSource.Token).Wait();
                }
            }

            Assert.AreEqual(100_002, _performCount);
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
            public int SomeId { get; set; }
            public string SomeName { get; set; }
        }

        private class DummyJobArgs2
        {
            public int SomeId2 { get; set; }
            public string SomeName2 { get; set; }
        }
    }
}
