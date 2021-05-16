using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.EntityFrameworkCore;

using MassiveJobs.Core;

namespace MassiveJobs.SqlServerBroker.Tests
{
    [TestClass]
    public class UnitTest1
    {
        private List<TestTimer> _timers;
        private TestTimeProvider _timeProvider;

        [TestInitialize]
        public void TestInit()
        {
            _timers = new List<TestTimer>();
            _timeProvider = new TestTimeProvider();

            using (var db = new TestDbContext())
            {
                //db.Database.EnsureDeleted();
                //db.Database.EnsureCreated();
                db.Database.ExecuteSqlRaw("delete from massive_jobs.message_queue");
                db.Database.ExecuteSqlRaw("delete from massive_jobs.single_consumer_lock");
            }

            JobsBuilder.Configure()
                .WithSettings("tests.", s =>
                {
                    s.PublishBatchSize = 300;
                    s.ImmediateWorkersBatchSize = 1000;
                    s.MaxDegreeOfParallelismPerWorker = 2;
                    s.ImmediateWorkersCount = 2;
                    s.ScheduledWorkersCount = 2;
                    s.PeriodicWorkersCount = 2;
                })
                .RegisterInstance<ITimeProvider>(_timeProvider)
                .RegisterScoped<ITimer>(f =>
                {
                    var timer = new TestTimer();
                    _timers.Add(timer);
                    return timer;
                })
                .WithSqlServerBroker<TestDbContext>()
                .RegisterScoped<TestDbContext, TestDbContext>()
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

            FireActiveTimers();
        }

        private void StartWorkers()
        {
            var task = Task.Factory.StartNew(MassiveJobsMediator.DefaultInstance.StartJobWorkers);

            while (!task.IsCompleted)
            {
                FireActiveTimers();
            }

            FireActiveTimers();
        }

        private void StopWorkers()
        {
            var task = Task.Factory.StartNew(MassiveJobsMediator.DefaultInstance.StopJobWorkers);

            while (!task.IsCompleted)
            {
                FireActiveTimers();
            }

            FireActiveTimers();
        }

        private void FireActiveTimers()
        {
            for (var i = 0; i < _timers.Count; i++) 
            {
                _timers[i]?.FireTimeElapsedIfActive();
            }
        }

        [TestMethod]
        public void TestMethod1()
        {
            _timeProvider.CurrentTimeUtc = new DateTime(2021, 5, 15, 23, 20, 0, DateTimeKind.Utc);

            MockJob.ResetPerformCount();

            MockJob.Publish();

            StopWorkers();
            Assert.AreEqual(1, MockJob.PerformCount);
            StartWorkers();

            MockJob.PublishPeriodic("test", 10, _timeProvider.CurrentTimeUtc, _timeProvider.CurrentTimeUtc.AddSeconds(35));

            _timeProvider.AdvanceTime(9999);
            FireActiveTimers();

            StopWorkers();
            Assert.AreEqual(1, MockJob.PerformCount);
            StartWorkers();

            _timeProvider.AdvanceTime(1);
            FireActiveTimers();

            StopWorkers();
            Assert.AreEqual(2, MockJob.PerformCount);
            StartWorkers();

            _timeProvider.AdvanceTime(10000);
            FireActiveTimers();

            StopWorkers();
            Assert.AreEqual(3, MockJob.PerformCount);
            StartWorkers();

            _timeProvider.AdvanceTime(10000);
            FireActiveTimers();

            StopWorkers();
            Assert.AreEqual(4, MockJob.PerformCount);
            StartWorkers();

            _timeProvider.AdvanceTime(10000);
            FireActiveTimers();

            StopWorkers();
            Assert.AreEqual(4, MockJob.PerformCount);
        }
    }
}
