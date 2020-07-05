using System.Collections;

namespace MassiveJobs.Core.Cron
{
    static class BitArrayExtensions
    {
        public static int GetNextSetBit(this BitArray bits, int startIndex)
        {
            for (var i = startIndex; i < bits.Count; i++)
            {
                if (bits.Get(i)) return i;
            }

            return -1;
        }
    }
}
