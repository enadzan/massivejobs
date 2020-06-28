using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Threading;

using MassiveJobs.Core;

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
                HostNames = new[] { "localhost" },
                VirtualHost = "massivejobs.tests",
                Username = "guest",
                Password = "guest",
                NamePrefix = "tests."
            };

            _jobs = RabbitMqJobsBuilder
                .FromSettings(_settings)
                .Configure(s =>
                {
                    s.PublishBatchSize = 400;
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
            var jobArgs = new List<DummyJobArgs>();

            for (var i = 0; i < 10000; i++) {
                jobArgs.Add(new DummyJobArgs { SomeId = i + 1, SomeName = "Meho" });
            }

            _jobs.StartJobWorkers();

            _jobs.Publish<DummyJob, DummyJobArgs>(jobArgs, null);
            _jobs.Publish<DummyJob, DummyJobArgs2>(new DummyJobArgs2());

            Thread.Sleep(1000);

            Assert.AreEqual(10002, _performCount);
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
