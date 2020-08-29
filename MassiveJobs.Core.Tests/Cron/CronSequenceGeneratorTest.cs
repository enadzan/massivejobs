using MassiveJobs.Core.Cron;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Runtime.InteropServices;

namespace MassiveJobs.Core.Tests.Cron
{
    [TestClass]
    public class CronSequenceGeneratorTest
    {
        private static readonly TimeZoneInfo SarajevoTimeZone;

        static CronSequenceGeneratorTest()
        {
            SarajevoTimeZone = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                ? TimeZoneInfo.FindSystemTimeZoneById("Central European Standard Time")
                : TimeZoneInfo.FindSystemTimeZoneById("Europe/Sarajevo");
        }

        [TestMethod]
        public void Expression_should_run_every_second()
        {
            var cronGen = new CronSequenceGenerator("* * * ? * *", SarajevoTimeZone);

            var now = new DateTime(2020, 7, 5, 13, 50, 0, 0);
            var nowUtc = TimeZoneInfo.ConvertTimeToUtc(now, SarajevoTimeZone);

            for (var i = 0; i < 10; i++)
            {
                var nextTime = TimeZoneInfo.ConvertTimeFromUtc(cronGen.NextUtc(nowUtc), SarajevoTimeZone);
                Assert.AreEqual(now.AddSeconds(i + 1), nextTime);

                nowUtc = TimeZoneInfo.ConvertTimeToUtc(nextTime, SarajevoTimeZone);
            }
        }

        [TestMethod]
        public void Expression_should_skip_invalid_time()
        {
            var cronGen = new CronSequenceGenerator("* * * ? * *", SarajevoTimeZone);

            var now = new DateTime(2020, 3, 29, 01, 59, 59, 0);
            var nowUtc = TimeZoneInfo.ConvertTimeToUtc(now, SarajevoTimeZone);

            for (var i = 0; i < 10; i++)
            {
                var nextTime = TimeZoneInfo.ConvertTimeFromUtc(cronGen.NextUtc(nowUtc), SarajevoTimeZone);
                Assert.AreEqual(now.AddHours(1).AddSeconds(i + 1), nextTime);

                nowUtc = TimeZoneInfo.ConvertTimeToUtc(nextTime, SarajevoTimeZone);
            }
        }

        [TestMethod]
        public void Expression_should_handle_ambiguous_time_exit()
        {
            var cronGen = new CronSequenceGenerator("* * * ? * *", SarajevoTimeZone);

            var nowUtc = new DateTime(2020, 10, 25, 01, 59, 59, 0, DateTimeKind.Utc);
            var originalUtc = nowUtc;

            for (var i = 0; i < 10; i++)
            {
                var nextTimeUtc = cronGen.NextUtc(nowUtc);
                Assert.AreEqual(originalUtc.AddSeconds(i + 1), nextTimeUtc);

                nowUtc = nextTimeUtc;
            }
        }

        [TestMethod]
        public void Expression_should_handle_ambiguous_time_entry()
        {
            var cronGen = new CronSequenceGenerator("* * * ? * *", SarajevoTimeZone);

            var nowUtc = new DateTime(2020, 10, 24, 23, 59, 59, 0, DateTimeKind.Utc);
            var originalUtc = nowUtc;

            for (var i = 0; i < 10; i++)
            {
                var nextTimeUtc = cronGen.NextUtc(nowUtc);
                Assert.AreEqual(originalUtc.AddSeconds(i + 1), nextTimeUtc);

                nowUtc = nextTimeUtc;
            }
        }

        [TestMethod]
        public void Expression_should_not_fire_twice_in_ambiguous_time()
        {
            var cronGen = new CronSequenceGenerator("0 0,30 2 ? * *", SarajevoTimeZone);

            var nowUtc = new DateTime(2020, 10, 24, 23, 0, 0, 0, DateTimeKind.Utc);

            var nextTimeUtc = cronGen.NextUtc(nowUtc);
            Assert.AreEqual( new DateTime(2020, 10, 25, 0, 0, 0, 0, DateTimeKind.Utc), nextTimeUtc);

            nowUtc = nextTimeUtc;

            nextTimeUtc = cronGen.NextUtc(nowUtc);
            Assert.AreEqual( new DateTime(2020, 10, 25, 0, 30, 0, 0, DateTimeKind.Utc), nextTimeUtc);

            nowUtc = nextTimeUtc;

            nextTimeUtc = cronGen.NextUtc(nowUtc);
            Assert.AreEqual( new DateTime(2020, 10, 26, 1, 0, 0, 0, DateTimeKind.Utc), nextTimeUtc);
        }

        [TestMethod]
        public void Expression_should_handle_periodic_ambiguous_time()
        {
            var cronGen = new CronSequenceGenerator("0 * 2 ? * *", SarajevoTimeZone);

            var nowUtc = new DateTime(2020, 10, 24, 23, 0, 0, 0, DateTimeKind.Utc);
            var beginUtc = new DateTime(2020, 10, 25, 0, 0, 0, 0, DateTimeKind.Utc);

            DateTime nextTimeUtc;

            for (var i = 0; i < 120; i++) // test both ambiguous periods
            {
                nextTimeUtc = cronGen.NextUtc(nowUtc);
                Assert.AreEqual(beginUtc.AddMinutes(i), nextTimeUtc);

                nowUtc = nextTimeUtc;
            }

            nextTimeUtc = cronGen.NextUtc(nowUtc);
            Assert.AreEqual(new DateTime(2020, 10, 26, 1, 0, 0, 0, DateTimeKind.Utc), nextTimeUtc);
        }

        [TestMethod]
        public void Expression_should_run_every_even_minute()
        {
            var cronGen = new CronSequenceGenerator("0 */2 * ? * *", SarajevoTimeZone);

            var now = new DateTime(2020, 7, 5, 13, 50, 0, 0);
            var nowUtc = TimeZoneInfo.ConvertTimeToUtc(now, SarajevoTimeZone);

            DateTime nextTime;

            nextTime = TimeZoneInfo.ConvertTimeFromUtc(cronGen.NextUtc(nowUtc), SarajevoTimeZone);
            Assert.AreEqual(new DateTime(2020, 7, 5, 13, 52, 0, 0), nextTime);

            nowUtc = TimeZoneInfo.ConvertTimeToUtc(nextTime, SarajevoTimeZone);

            nextTime = TimeZoneInfo.ConvertTimeFromUtc(cronGen.NextUtc(nowUtc), SarajevoTimeZone);
            Assert.AreEqual(new DateTime(2020, 7, 5, 13, 54, 0, 0), nextTime);

            nowUtc = TimeZoneInfo.ConvertTimeToUtc(nextTime, SarajevoTimeZone);

            nextTime = TimeZoneInfo.ConvertTimeFromUtc(cronGen.NextUtc(nowUtc), SarajevoTimeZone);
            Assert.AreEqual(new DateTime(2020, 7, 5, 13, 56, 0, 0), nextTime);

            nowUtc = TimeZoneInfo.ConvertTimeToUtc(nextTime, SarajevoTimeZone);

            nextTime = TimeZoneInfo.ConvertTimeFromUtc(cronGen.NextUtc(nowUtc), SarajevoTimeZone);
            Assert.AreEqual(new DateTime(2020, 7, 5, 13, 58, 0, 0), nextTime);

            nowUtc = TimeZoneInfo.ConvertTimeToUtc(nextTime, SarajevoTimeZone);

            nextTime = TimeZoneInfo.ConvertTimeFromUtc(cronGen.NextUtc(nowUtc), SarajevoTimeZone);
            Assert.AreEqual(new DateTime(2020, 7, 5, 14, 0, 0, 0), nextTime);

            nowUtc = TimeZoneInfo.ConvertTimeToUtc(nextTime, SarajevoTimeZone);

            nextTime = TimeZoneInfo.ConvertTimeFromUtc(cronGen.NextUtc(nowUtc), SarajevoTimeZone);
            Assert.AreEqual(new DateTime(2020, 7, 5, 14, 2, 0, 0), nextTime);
        }

        [TestMethod]
        public void Expression_should_run_every_uneven_minute()
        {
            var cronGen = new CronSequenceGenerator("0 1/2 * ? * *", SarajevoTimeZone);

            var now = new DateTime(2020, 7, 5, 13, 50, 0, 0);
            var nowUtc = TimeZoneInfo.ConvertTimeToUtc(now, SarajevoTimeZone);

            DateTime nextTime;

            nextTime = TimeZoneInfo.ConvertTimeFromUtc(cronGen.NextUtc(nowUtc), SarajevoTimeZone);
            Assert.AreEqual(new DateTime(2020, 7, 5, 13, 51, 0, 0), nextTime);

            nowUtc = TimeZoneInfo.ConvertTimeToUtc(nextTime, SarajevoTimeZone);

            nextTime = TimeZoneInfo.ConvertTimeFromUtc(cronGen.NextUtc(nowUtc), SarajevoTimeZone);
            Assert.AreEqual(new DateTime(2020, 7, 5, 13, 53, 0, 0), nextTime);

            nowUtc = TimeZoneInfo.ConvertTimeToUtc(nextTime, SarajevoTimeZone);

            nextTime = TimeZoneInfo.ConvertTimeFromUtc(cronGen.NextUtc(nowUtc), SarajevoTimeZone);
            Assert.AreEqual(new DateTime(2020, 7, 5, 13, 55, 0, 0), nextTime);

            nowUtc = TimeZoneInfo.ConvertTimeToUtc(nextTime, SarajevoTimeZone);

            nextTime = TimeZoneInfo.ConvertTimeFromUtc(cronGen.NextUtc(nowUtc), SarajevoTimeZone);
            Assert.AreEqual(new DateTime(2020, 7, 5, 13, 57, 0, 0), nextTime);

            nowUtc = TimeZoneInfo.ConvertTimeToUtc(nextTime, SarajevoTimeZone);

            nextTime = TimeZoneInfo.ConvertTimeFromUtc(cronGen.NextUtc(nowUtc), SarajevoTimeZone);
            Assert.AreEqual(new DateTime(2020, 7, 5, 13, 59, 0, 0), nextTime);

            nowUtc = TimeZoneInfo.ConvertTimeToUtc(nextTime, SarajevoTimeZone);

            nextTime = TimeZoneInfo.ConvertTimeFromUtc(cronGen.NextUtc(nowUtc), SarajevoTimeZone);
            Assert.AreEqual(new DateTime(2020, 7, 5, 14, 1, 0, 0), nextTime);
        }

        [TestMethod]
        public void Expression_should_run_every_13th_minute_in_an_hour()
        {
            // this means it will run at minutes 0, 13, 26, 39, 52
            var cronGen = new CronSequenceGenerator("0 */13 * ? * *", SarajevoTimeZone);

            var now = new DateTime(2020, 7, 5, 13, 50, 0, 0);
            var nowUtc = TimeZoneInfo.ConvertTimeToUtc(now, SarajevoTimeZone);

            DateTime nextTime;

            nextTime = TimeZoneInfo.ConvertTimeFromUtc(cronGen.NextUtc(nowUtc), SarajevoTimeZone);
            Assert.AreEqual(new DateTime(2020, 7, 5, 13, 52, 0, 0), nextTime);

            nowUtc = TimeZoneInfo.ConvertTimeToUtc(nextTime, SarajevoTimeZone);

            nextTime = TimeZoneInfo.ConvertTimeFromUtc(cronGen.NextUtc(nowUtc), SarajevoTimeZone);
            Assert.AreEqual(new DateTime(2020, 7, 5, 14, 0, 0, 0), nextTime);

            nowUtc = TimeZoneInfo.ConvertTimeToUtc(nextTime, SarajevoTimeZone);

            nextTime = TimeZoneInfo.ConvertTimeFromUtc(cronGen.NextUtc(nowUtc), SarajevoTimeZone);
            Assert.AreEqual(new DateTime(2020, 7, 5, 14, 13, 0, 0), nextTime);

            nowUtc = TimeZoneInfo.ConvertTimeToUtc(nextTime, SarajevoTimeZone);

            nextTime = TimeZoneInfo.ConvertTimeFromUtc(cronGen.NextUtc(nowUtc), SarajevoTimeZone);
            Assert.AreEqual(new DateTime(2020, 7, 5, 14, 26, 0, 0), nextTime);

            nowUtc = TimeZoneInfo.ConvertTimeToUtc(nextTime, SarajevoTimeZone);

            nextTime = TimeZoneInfo.ConvertTimeFromUtc(cronGen.NextUtc(nowUtc), SarajevoTimeZone);
            Assert.AreEqual(new DateTime(2020, 7, 5, 14, 39, 0, 0), nextTime);

            nowUtc = TimeZoneInfo.ConvertTimeToUtc(nextTime, SarajevoTimeZone);

            nextTime = TimeZoneInfo.ConvertTimeFromUtc(cronGen.NextUtc(nowUtc), SarajevoTimeZone);
            Assert.AreEqual(new DateTime(2020, 7, 5, 14, 52, 0, 0), nextTime);
        }

        [TestMethod]
        public void Expression_should_run_every_hour_at_15_30_40min()
        {
            var cronGen = new CronSequenceGenerator("0 15,30,45 * ? * *", SarajevoTimeZone);

            var now = new DateTime(2020, 7, 5, 13, 50, 0, 0);
            var nowUtc = TimeZoneInfo.ConvertTimeToUtc(now, SarajevoTimeZone);

            DateTime nextTime;

            nextTime = TimeZoneInfo.ConvertTimeFromUtc(cronGen.NextUtc(nowUtc), SarajevoTimeZone);
            Assert.AreEqual(new DateTime(2020, 7, 5, 14, 15, 0, 0), nextTime);

            nowUtc = TimeZoneInfo.ConvertTimeToUtc(nextTime, SarajevoTimeZone);

            nextTime = TimeZoneInfo.ConvertTimeFromUtc(cronGen.NextUtc(nowUtc), SarajevoTimeZone);
            Assert.AreEqual(new DateTime(2020, 7, 5, 14, 30, 0, 0), nextTime);

            nowUtc = TimeZoneInfo.ConvertTimeToUtc(nextTime, SarajevoTimeZone);

            nextTime = TimeZoneInfo.ConvertTimeFromUtc(cronGen.NextUtc(nowUtc), SarajevoTimeZone);
            Assert.AreEqual(new DateTime(2020, 7, 5, 14, 45, 0, 0), nextTime);

            nowUtc = TimeZoneInfo.ConvertTimeToUtc(nextTime, SarajevoTimeZone);

            nextTime = TimeZoneInfo.ConvertTimeFromUtc(cronGen.NextUtc(nowUtc), SarajevoTimeZone);
            Assert.AreEqual(new DateTime(2020, 7, 5, 15, 15, 0, 0), nextTime);

            nowUtc = TimeZoneInfo.ConvertTimeToUtc(nextTime, SarajevoTimeZone);

            nextTime = TimeZoneInfo.ConvertTimeFromUtc(cronGen.NextUtc(nowUtc), SarajevoTimeZone);
            Assert.AreEqual(new DateTime(2020, 7, 5, 15, 30, 0, 0), nextTime);

            nowUtc = TimeZoneInfo.ConvertTimeToUtc(nextTime, SarajevoTimeZone);

            nextTime = TimeZoneInfo.ConvertTimeFromUtc(cronGen.NextUtc(nowUtc), SarajevoTimeZone);
            Assert.AreEqual(new DateTime(2020, 7, 5, 15, 45, 0, 0), nextTime);
        }

        [TestMethod]
        public void Expression_should_run_at_0_15_30_40min_between_8am_9pm()
        {
            var cronGen = new CronSequenceGenerator("0 0,15,30,45 8-20 ? * *", SarajevoTimeZone);

            var now = new DateTime(2020, 7, 5, 19, 50, 0, 0);
            var nowUtc = TimeZoneInfo.ConvertTimeToUtc(now, SarajevoTimeZone);

            DateTime nextTime;

            nextTime = TimeZoneInfo.ConvertTimeFromUtc(cronGen.NextUtc(nowUtc), SarajevoTimeZone);
            Assert.AreEqual(new DateTime(2020, 7, 5, 20, 0, 0, 0), nextTime);

            nowUtc = TimeZoneInfo.ConvertTimeToUtc(nextTime, SarajevoTimeZone);

            nextTime = TimeZoneInfo.ConvertTimeFromUtc(cronGen.NextUtc(nowUtc), SarajevoTimeZone);
            Assert.AreEqual(new DateTime(2020, 7, 5, 20, 15, 0, 0), nextTime);

            nowUtc = TimeZoneInfo.ConvertTimeToUtc(nextTime, SarajevoTimeZone);

            nextTime = TimeZoneInfo.ConvertTimeFromUtc(cronGen.NextUtc(nowUtc), SarajevoTimeZone);
            Assert.AreEqual(new DateTime(2020, 7, 5, 20, 30, 0, 0), nextTime);

            nowUtc = TimeZoneInfo.ConvertTimeToUtc(nextTime, SarajevoTimeZone);

            nextTime = TimeZoneInfo.ConvertTimeFromUtc(cronGen.NextUtc(nowUtc), SarajevoTimeZone);
            Assert.AreEqual(new DateTime(2020, 7, 5, 20, 45, 0, 0), nextTime);

            nowUtc = TimeZoneInfo.ConvertTimeToUtc(nextTime, SarajevoTimeZone);

            nextTime = TimeZoneInfo.ConvertTimeFromUtc(cronGen.NextUtc(nowUtc), SarajevoTimeZone);
            Assert.AreEqual(new DateTime(2020, 7, 6, 8, 0, 0, 0), nextTime);

            nowUtc = TimeZoneInfo.ConvertTimeToUtc(nextTime, SarajevoTimeZone);

            nextTime = TimeZoneInfo.ConvertTimeFromUtc(cronGen.NextUtc(nowUtc), SarajevoTimeZone);
            Assert.AreEqual(new DateTime(2020, 7, 6, 8, 15, 0, 0), nextTime);
        }

        [TestMethod]
        public void Expression_should_run_every_hour()
        {
            var cronGen = new CronSequenceGenerator("0 0 * ? * *", SarajevoTimeZone);

            var now = new DateTime(2020, 7, 5, 13, 50, 0, 0);
            var nowUtc = TimeZoneInfo.ConvertTimeToUtc(now, SarajevoTimeZone);

            DateTime nextTime;

            nextTime = TimeZoneInfo.ConvertTimeFromUtc(cronGen.NextUtc(nowUtc), SarajevoTimeZone);
            Assert.AreEqual(new DateTime(2020, 7, 5, 14, 0, 0, 0), nextTime);

            nowUtc = TimeZoneInfo.ConvertTimeToUtc(nextTime, SarajevoTimeZone);

            nextTime = TimeZoneInfo.ConvertTimeFromUtc(cronGen.NextUtc(nowUtc), SarajevoTimeZone);
            Assert.AreEqual(new DateTime(2020, 7, 5, 15, 0, 0, 0), nextTime);

            nowUtc = TimeZoneInfo.ConvertTimeToUtc(nextTime, SarajevoTimeZone);

            nextTime = TimeZoneInfo.ConvertTimeFromUtc(cronGen.NextUtc(nowUtc), SarajevoTimeZone);
            Assert.AreEqual(new DateTime(2020, 7, 5, 16, 0, 0, 0), nextTime);

            nowUtc = TimeZoneInfo.ConvertTimeToUtc(nextTime, SarajevoTimeZone);

            nextTime = TimeZoneInfo.ConvertTimeFromUtc(cronGen.NextUtc(nowUtc), SarajevoTimeZone);
            Assert.AreEqual(new DateTime(2020, 7, 5, 17, 0, 0, 0), nextTime);

            nowUtc = TimeZoneInfo.ConvertTimeToUtc(nextTime, SarajevoTimeZone);

            nextTime = TimeZoneInfo.ConvertTimeFromUtc(cronGen.NextUtc(nowUtc), SarajevoTimeZone);
            Assert.AreEqual(new DateTime(2020, 7, 5, 18, 0, 0, 0), nextTime);

            nowUtc = TimeZoneInfo.ConvertTimeToUtc(nextTime, SarajevoTimeZone);

            nextTime = TimeZoneInfo.ConvertTimeFromUtc(cronGen.NextUtc(nowUtc), SarajevoTimeZone);
            Assert.AreEqual(new DateTime(2020, 7, 5, 19, 0, 0, 0), nextTime);
        }

        [TestMethod]
        public void Expression_should_run_every_12_hours()
        {
            var cronGen = new CronSequenceGenerator("0 0 */12 ? * *", SarajevoTimeZone);

            var now = new DateTime(2020, 7, 5, 13, 50, 0, 0);
            var nowUtc = TimeZoneInfo.ConvertTimeToUtc(now, SarajevoTimeZone);

            DateTime nextTime;

            nextTime = TimeZoneInfo.ConvertTimeFromUtc(cronGen.NextUtc(nowUtc), SarajevoTimeZone);
            Assert.AreEqual(new DateTime(2020, 7, 6, 0, 0, 0, 0), nextTime);

            nowUtc = TimeZoneInfo.ConvertTimeToUtc(nextTime, SarajevoTimeZone);

            nextTime = TimeZoneInfo.ConvertTimeFromUtc(cronGen.NextUtc(nowUtc), SarajevoTimeZone);
            Assert.AreEqual(new DateTime(2020, 7, 6, 12, 0, 0, 0), nextTime);

            nowUtc = TimeZoneInfo.ConvertTimeToUtc(nextTime, SarajevoTimeZone);

            nextTime = TimeZoneInfo.ConvertTimeFromUtc(cronGen.NextUtc(nowUtc), SarajevoTimeZone);
            Assert.AreEqual(new DateTime(2020, 7, 7, 0, 0, 0, 0), nextTime);

            nowUtc = TimeZoneInfo.ConvertTimeToUtc(nextTime, SarajevoTimeZone);

            nextTime = TimeZoneInfo.ConvertTimeFromUtc(cronGen.NextUtc(nowUtc), SarajevoTimeZone);
            Assert.AreEqual(new DateTime(2020, 7, 7, 12, 0, 0, 0), nextTime);

            nowUtc = TimeZoneInfo.ConvertTimeToUtc(nextTime, SarajevoTimeZone);

            nextTime = TimeZoneInfo.ConvertTimeFromUtc(cronGen.NextUtc(nowUtc), SarajevoTimeZone);
            Assert.AreEqual(new DateTime(2020, 7, 8, 0, 0, 0, 0), nextTime);

            nowUtc = TimeZoneInfo.ConvertTimeToUtc(nextTime, SarajevoTimeZone);

            nextTime = TimeZoneInfo.ConvertTimeFromUtc(cronGen.NextUtc(nowUtc), SarajevoTimeZone);
            Assert.AreEqual(new DateTime(2020, 7, 8, 12, 0, 0, 0), nextTime);
        }

        [TestMethod]
        public void Expression_should_run_every_day_at_6am()
        {
            var cronGen = new CronSequenceGenerator("0 0 6 * * ?", SarajevoTimeZone);

            var now = new DateTime(2020, 6, 29, 13, 50, 0, 0);
            var nowUtc = TimeZoneInfo.ConvertTimeToUtc(now, SarajevoTimeZone);

            DateTime nextTime;

            nextTime = TimeZoneInfo.ConvertTimeFromUtc(cronGen.NextUtc(nowUtc), SarajevoTimeZone);
            Assert.AreEqual(new DateTime(2020, 6, 30, 6, 0, 0, 0), nextTime);

            nowUtc = TimeZoneInfo.ConvertTimeToUtc(nextTime, SarajevoTimeZone);

            nextTime = TimeZoneInfo.ConvertTimeFromUtc(cronGen.NextUtc(nowUtc), SarajevoTimeZone);
            Assert.AreEqual(new DateTime(2020, 7, 1, 6, 0, 0, 0), nextTime);

            nowUtc = TimeZoneInfo.ConvertTimeToUtc(nextTime, SarajevoTimeZone);

            nextTime = TimeZoneInfo.ConvertTimeFromUtc(cronGen.NextUtc(nowUtc), SarajevoTimeZone);
            Assert.AreEqual(new DateTime(2020, 7, 2, 6, 0, 0, 0), nextTime);

            nowUtc = TimeZoneInfo.ConvertTimeToUtc(nextTime, SarajevoTimeZone);

            nextTime = TimeZoneInfo.ConvertTimeFromUtc(cronGen.NextUtc(nowUtc), SarajevoTimeZone);
            Assert.AreEqual(new DateTime(2020, 7, 3, 6, 0, 0, 0), nextTime);

            nowUtc = TimeZoneInfo.ConvertTimeToUtc(nextTime, SarajevoTimeZone);

            nextTime = TimeZoneInfo.ConvertTimeFromUtc(cronGen.NextUtc(nowUtc), SarajevoTimeZone);
            Assert.AreEqual(new DateTime(2020, 7, 4, 6, 0, 0, 0), nextTime);

            nowUtc = TimeZoneInfo.ConvertTimeToUtc(nextTime, SarajevoTimeZone);

            nextTime = TimeZoneInfo.ConvertTimeFromUtc(cronGen.NextUtc(nowUtc), SarajevoTimeZone);
            Assert.AreEqual(new DateTime(2020, 7, 5, 6, 0, 0, 0), nextTime);
        }

        [TestMethod]
        public void Expression_should_run_every_sunday_at_noon()
        {
            var cronGen = new CronSequenceGenerator("0 0 12 * * SUN", SarajevoTimeZone);

            var now = new DateTime(2020, 12, 11, 13, 50, 59, 234);
            var nowUtc = TimeZoneInfo.ConvertTimeToUtc(now, SarajevoTimeZone);

            DateTime nextTime;

            nextTime = TimeZoneInfo.ConvertTimeFromUtc(cronGen.NextUtc(nowUtc), SarajevoTimeZone);
            Assert.AreEqual(new DateTime(2020, 12, 13, 12, 0, 0, 0), nextTime);

            nowUtc = TimeZoneInfo.ConvertTimeToUtc(nextTime, SarajevoTimeZone);

            nextTime = TimeZoneInfo.ConvertTimeFromUtc(cronGen.NextUtc(nowUtc), SarajevoTimeZone);
            Assert.AreEqual(new DateTime(2020, 12, 20, 12, 0, 0, 0), nextTime);

            nowUtc = TimeZoneInfo.ConvertTimeToUtc(nextTime, SarajevoTimeZone);

            nextTime = TimeZoneInfo.ConvertTimeFromUtc(cronGen.NextUtc(nowUtc), SarajevoTimeZone);
            Assert.AreEqual(new DateTime(2020, 12, 27, 12, 0, 0, 0), nextTime);

            nowUtc = TimeZoneInfo.ConvertTimeToUtc(nextTime, SarajevoTimeZone);

            nextTime = TimeZoneInfo.ConvertTimeFromUtc(cronGen.NextUtc(nowUtc), SarajevoTimeZone);
            Assert.AreEqual(new DateTime(2021, 1, 3, 12, 0, 0, 0), nextTime);

            nowUtc = TimeZoneInfo.ConvertTimeToUtc(nextTime, SarajevoTimeZone);

            nextTime = TimeZoneInfo.ConvertTimeFromUtc(cronGen.NextUtc(nowUtc), SarajevoTimeZone);
            Assert.AreEqual(new DateTime(2021, 1, 10, 12, 0, 0, 0), nextTime);

            nowUtc = TimeZoneInfo.ConvertTimeToUtc(nextTime, SarajevoTimeZone);

            nextTime = TimeZoneInfo.ConvertTimeFromUtc(cronGen.NextUtc(nowUtc), SarajevoTimeZone);
            Assert.AreEqual(new DateTime(2021, 1, 17, 12, 0, 0, 0), nextTime);
        }

        [TestMethod]
        public void Expression_should_run_every_weekday_at_noon()
        {
            var cronGen = new CronSequenceGenerator("0 0 12 * * MON-FRI", SarajevoTimeZone);

            var now = new DateTime(2020, 7, 5, 13, 50, 0, 0);
            var nowUtc = TimeZoneInfo.ConvertTimeToUtc(now, SarajevoTimeZone);

            DateTime nextTime;

            nextTime = TimeZoneInfo.ConvertTimeFromUtc(cronGen.NextUtc(nowUtc), SarajevoTimeZone);
            Assert.AreEqual(new DateTime(2020, 7, 6, 12, 0, 0, 0), nextTime);

            nowUtc = TimeZoneInfo.ConvertTimeToUtc(nextTime, SarajevoTimeZone);

            nextTime = TimeZoneInfo.ConvertTimeFromUtc(cronGen.NextUtc(nowUtc), SarajevoTimeZone);
            Assert.AreEqual(new DateTime(2020, 7, 7, 12, 0, 0, 0), nextTime);

            nowUtc = TimeZoneInfo.ConvertTimeToUtc(nextTime, SarajevoTimeZone);

            nextTime = TimeZoneInfo.ConvertTimeFromUtc(cronGen.NextUtc(nowUtc), SarajevoTimeZone);
            Assert.AreEqual(new DateTime(2020, 7, 8, 12, 0, 0, 0), nextTime);

            nowUtc = TimeZoneInfo.ConvertTimeToUtc(nextTime, SarajevoTimeZone);

            nextTime = TimeZoneInfo.ConvertTimeFromUtc(cronGen.NextUtc(nowUtc), SarajevoTimeZone);
            Assert.AreEqual(new DateTime(2020, 7, 9, 12, 0, 0, 0), nextTime);

            nowUtc = TimeZoneInfo.ConvertTimeToUtc(nextTime, SarajevoTimeZone);

            nextTime = TimeZoneInfo.ConvertTimeFromUtc(cronGen.NextUtc(nowUtc), SarajevoTimeZone);
            Assert.AreEqual(new DateTime(2020, 7, 10, 12, 0, 0, 0), nextTime);

            nowUtc = TimeZoneInfo.ConvertTimeToUtc(nextTime, SarajevoTimeZone);

            nextTime = TimeZoneInfo.ConvertTimeFromUtc(cronGen.NextUtc(nowUtc), SarajevoTimeZone);
            Assert.AreEqual(new DateTime(2020, 7, 13, 12, 0, 0, 0), nextTime);
        }

        [TestMethod]
        public void Expression_should_run_every_1st_of_March_June_Sep_Dec_at_2am()
        {
            var cronGen = new CronSequenceGenerator("0 0 2 1 MAR,JUN,SEP,DEC ?", SarajevoTimeZone);

            var now = new DateTime(2020, 7, 5, 13, 50, 0, 0);
            var nowUtc = TimeZoneInfo.ConvertTimeToUtc(now, SarajevoTimeZone);

            DateTime nextTime;

            nextTime = TimeZoneInfo.ConvertTimeFromUtc(cronGen.NextUtc(nowUtc), SarajevoTimeZone);
            Assert.AreEqual(new DateTime(2020, 9, 1, 2, 0, 0, 0), nextTime);

            nowUtc = TimeZoneInfo.ConvertTimeToUtc(nextTime, SarajevoTimeZone);

            nextTime = TimeZoneInfo.ConvertTimeFromUtc(cronGen.NextUtc(nowUtc), SarajevoTimeZone);
            Assert.AreEqual(new DateTime(2020, 12, 1, 2, 0, 0, 0), nextTime);

            nowUtc = TimeZoneInfo.ConvertTimeToUtc(nextTime, SarajevoTimeZone);

            nextTime = TimeZoneInfo.ConvertTimeFromUtc(cronGen.NextUtc(nowUtc), SarajevoTimeZone);
            Assert.AreEqual(new DateTime(2021, 3, 1, 2, 0, 0, 0), nextTime);

            nowUtc = TimeZoneInfo.ConvertTimeToUtc(nextTime, SarajevoTimeZone);

            nextTime = TimeZoneInfo.ConvertTimeFromUtc(cronGen.NextUtc(nowUtc), SarajevoTimeZone);
            Assert.AreEqual(new DateTime(2021, 6, 1, 2, 0, 0, 0), nextTime);

            nowUtc = TimeZoneInfo.ConvertTimeToUtc(nextTime, SarajevoTimeZone);

            nextTime = TimeZoneInfo.ConvertTimeFromUtc(cronGen.NextUtc(nowUtc), SarajevoTimeZone);
            Assert.AreEqual(new DateTime(2021, 9, 1, 2, 0, 0, 0), nextTime);

            nowUtc = TimeZoneInfo.ConvertTimeToUtc(nextTime, SarajevoTimeZone);

            nextTime = TimeZoneInfo.ConvertTimeFromUtc(cronGen.NextUtc(nowUtc), SarajevoTimeZone);
            Assert.AreEqual(new DateTime(2021, 12, 1, 2, 0, 0, 0), nextTime);
        }

        [TestMethod]
        public void TestAustralianTime()
        {
            var brisbaneTimeZoneInfo = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                ? TimeZoneInfo.FindSystemTimeZoneById("E. Australia Standard Time")
                : TimeZoneInfo.FindSystemTimeZoneById("Australia/Brisbane");

            // Australia has no daylight saving time shifts
            var cronGen = new CronSequenceGenerator("0 0 21 * * ?", brisbaneTimeZoneInfo);

            var lastTimeUtc = TimeZoneInfo.ConvertTimeToUtc(new DateTime(2020, 1, 1, 0, 0, 0, 0), brisbaneTimeZoneInfo);
            var expectedTime = new DateTime(2020, 1, 1, 21, 0, 0, 0);

            for (var i = 0; i < 365; i++)
            {
                lastTimeUtc = cronGen.NextUtc(lastTimeUtc);
                var actualTime = TimeZoneInfo.ConvertTimeFromUtc(lastTimeUtc, brisbaneTimeZoneInfo);

                Assert.AreEqual(expectedTime, actualTime);

                expectedTime = expectedTime.AddDays(1);
            }
        }
    }
}
