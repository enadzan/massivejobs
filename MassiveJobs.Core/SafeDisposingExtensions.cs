using System;

namespace MassiveJobs.Core
{
    public static class SafeDisposingExtensions
    {
        public static void SafeDispose(this IDisposable disposable, IJobLogger logger = null)
        {
            try
            {
                if (disposable == null) return;

                if (logger?.IsEnabled(JobLogLevel.Debug) ?? false) logger.LogDebug($"Disposing {disposable.GetType()}");

                disposable.Dispose();

                if (logger?.IsEnabled(JobLogLevel.Debug) ?? false) logger.LogDebug($"Disposed {disposable.GetType()}");
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, "Disposal error");
            }
        }
    }
}
