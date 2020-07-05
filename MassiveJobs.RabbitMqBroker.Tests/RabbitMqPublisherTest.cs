using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
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

        private Jobs _jobs;

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

            _jobs = RabbitMqJobsBuilder
                .FromSettings(_settings)
                .Configure(s =>
                {
                    s.PublishBatchSize = 400;
                    s.ImmediateWorkersCount = 3;
                    s.ScheduledWorkersCount = 2;
                    s.PeriodicWorkersCount = 2;
                })
                .Build();
        }

        [TestCleanup]
        public void TestCleanup()
        {
            _jobs.SafeDispose();
        }

        [TestMethod]
        public void Publish_should_not_throw_exception()
        {
            _jobs.StartJobWorkers();

            var jobArgs = new List<DummyJobArgs>();

            for (var i = 0; i < 100_000; i++)
            {
                jobArgs.Add(new DummyJobArgs { SomeId = i });
            }

            _jobs.Publish<DummyJob, DummyJobArgs>(jobArgs, null);
            _jobs.Publish<DummyJob, DummyJobArgs2>(new DummyJobArgs2());

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
            _jobs.StartJobWorkers();

            Thread.Sleep(1000);

            var endAtUtc = DateTime.UtcNow.AddSeconds(4.5);

            _jobs.PublishPeriodic<DummyJob>("test_periodic", 1, null, endAtUtc);
            _jobs.PublishPeriodic<DummyJob>("test_periodic", 1, null, endAtUtc);
            _jobs.PublishPeriodic<DummyJob>("test_periodic", 1, null, endAtUtc);

            Thread.Sleep(6000);

            Assert.AreEqual(4, _performCount);
        }

        [TestMethod]
        public void CronJob_should_run_jobs_periodically_until_end()
        {
            _jobs.StartJobWorkers();

            var endAtUtc = DateTime.UtcNow.AddSeconds(4);

            _jobs.PublishPeriodic<DummyJob>("test_periodic", "0/2 * * ? * *", null, null, endAtUtc);

            // these should be ignored
            _jobs.PublishPeriodic<DummyJob>("test_periodic", 1, null, endAtUtc);
            _jobs.PublishPeriodic<DummyJob>("test_periodic", 1, null, endAtUtc);

            Thread.Sleep(6000);

            Assert.AreEqual(2, _performCount);
        }

        private class DummyJob
        {
            public void Perform()
            {
                Interlocked.Increment(ref _performCount);
                System.Diagnostics.Debug.WriteLine(DateTime.UtcNow);
            }

            public void Perform(DummyJobArgs _)
            {
                Interlocked.Increment(ref _performCount);
            }

            public void Perform(DummyJobArgs2 _)
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
