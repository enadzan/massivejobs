using Microsoft.VisualStudio.TestTools.UnitTesting;

using MassiveJobs.Core;
using System;

namespace MassiveJobs.SqlServerBroker.Tests
{
    [TestClass]
    public class UnitTest1
    {
        [TestMethod]
        public void TestMethod1()
        {
            using (var db = new TestDbContext())
            {
                db.Database.EnsureDeleted();
                db.Database.EnsureCreated();
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

            MockJob.ResetPerformCount();

            MockJob.Publish();

            MockJob.PerformedEvent.WaitOne(60000);

            Assert.AreEqual(1, MockJob.PerformCount);

            MockJob.PublishPeriodic("test", 10, null, DateTime.UtcNow.AddSeconds(35));

            MockJob.PerformedEvent.WaitOne(60000);
            MockJob.PerformedEvent.WaitOne(60000);
            MockJob.PerformedEvent.WaitOne(60000);

            Assert.AreEqual(4, MockJob.PerformCount);
        }
    }
}
