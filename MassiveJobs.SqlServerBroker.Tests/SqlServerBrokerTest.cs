using System;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.EntityFrameworkCore;

using MassiveJobs.Core;

namespace MassiveJobs.SqlServerBroker.Tests
{
    [TestClass]
    public class SqlServerBrokerTest
    {
        [TestInitialize]
        public void TestInit()
        {
            MockJob.ResetPerformCount();

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
                .WithSqlServerBroker<TestDbContext>()
                .RegisterScoped<TestDbContext, TestDbContext>()
                .Build();
        }

        [TestCleanup]
        public void TestCleanup()
        {
            JobsBuilder.DisposeJobs();
        }

        [TestMethod]
        public void TestPublish()
        {
            MockJob.Publish();

            Thread.Sleep(3000);
            Assert.AreEqual(1, MockJob.PerformCount);
        }

        [TestMethod]
        public void TestPublishPeriodic()
        {
            // round to seconds
            var startTime = new DateTime(DateTime.UtcNow.Ticks / TimeSpan.TicksPerMillisecond / 1000 * 1000 * TimeSpan.TicksPerMillisecond, DateTimeKind.Utc);

            startTime = startTime.AddSeconds(2);
            var endTime = startTime.AddMilliseconds(4250);

            Console.WriteLine("Start Time: " + startTime);
            Console.WriteLine("End Time: " + endTime);

            MockJob.PublishPeriodic("test", 2, startTime, endTime);

            Thread.Sleep(7000);
            Assert.AreEqual(2, MockJob.PerformCount);
        }
    }
}
