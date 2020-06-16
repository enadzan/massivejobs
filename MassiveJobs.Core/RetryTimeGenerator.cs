using System;

namespace MassiveJobs.Core
{
    public static class RetryTimeGenerator
    {
        private static readonly Random Rnd = new Random();

        public static DateTime GetNextRetryTime(int retryCount)
        {
            int randomNum;
            lock (Rnd) { randomNum = Rnd.Next(30); }

            // stolen from Sidekiq :/
            var seconds = Math.Pow(retryCount, 4) + 15 + (randomNum * (retryCount + 1));

            return DateTime.UtcNow.AddSeconds(seconds);
        }
    }
}
