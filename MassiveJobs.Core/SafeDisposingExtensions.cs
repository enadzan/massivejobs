using System;

namespace MassiveJobs.Core
{
    public static class SafeDisposingExtensions
    {
        public static void SafeDispose(this IDisposable disposable)
        {
            try
            {
                disposable?.Dispose();
            }
            catch
            {
            }
        }
    }
}
