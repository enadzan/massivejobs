using MassiveJobs.Core;
using System.Linq;
using System.Threading;

namespace MassiveJobs.SqlServerBroker.Tests
{
    public class MockJob : Job<MockJob>
    {
        private static int _performCount;
        public static int PerformCount => _performCount;

        private readonly TestDbContext _ctx;

        public bool UseTransaction => true;

        public MockJob(TestDbContext ctx)
        {
            _ctx = ctx;
        }

        public override void Perform()
        {
            var messages = _ctx.Set<MessageQueue>()
                .Count();

            if (messages == 0) throw new System.Exception("Zero messages");

            Interlocked.Increment(ref _performCount);
        }

        public static void ResetPerformCount()
        {
            Interlocked.Exchange(ref _performCount, 0);
        }
    }
}
