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

        private IServiceScopeFactory _scopeFactory;
        private IServiceScope _scope;

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

            _scopeFactory = RabbitMqBrokerBuilder
                .FromSettings(_settings)
                .Configure(s =>
                {
                    s.PublishBatchSize = 400;
                })
                .GetScopeFactory();

            _scope = _scopeFactory.CreateScope();
        }

        [TestCleanup]
        public void TestCleanup()
        {
            _scope.SafeDispose();
            _scopeFactory.SafeDispose();
        }

        [TestMethod]
        public void Publish_should_not_throw_exception()
        {
            var workers = _scope.GetService<IWorkerCoordinator>();
            var publisher = _scope.GetService<IJobPublisher>();

            var jobArgs = new List<DummyJobArgs>();

            for (var i = 0; i < 10000; i++) {
                jobArgs.Add(new DummyJobArgs { SomeId = i + 1, SomeName = "Meho" });
            }

            workers.StartJobWorkers();

            publisher.Publish<DummyJob, DummyJobArgs>(jobArgs, null);
            publisher.Publish<DummyJob, DummyJobArgs2>(new DummyJobArgs2());

            Thread.Sleep(1000);

            Assert.AreEqual(10002, _performCount);
        }

        [TestMethod]
        public void PublishPeriodic_should_run_jobs_periodically_until_end()
        {
            var workers = _scope.GetService<IWorkerCoordinator>();
            var publisher = _scope.GetService<IJobPublisher>();
            
            workers.StartJobWorkers();

            Thread.Sleep(1000);

            publisher.PublishPeriodic<DummyJob>("test_periodic", 1, null, DateTime.UtcNow.AddSeconds(4.5));
            publisher.PublishPeriodic<DummyJob>("test_periodic", 1);

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
