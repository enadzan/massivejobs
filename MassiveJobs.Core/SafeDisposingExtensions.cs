using System;

namespace MassiveJobs.Core
{
    public static class SafeDisposingExtensions
    {
        public static void SafeDispose(this IDisposable disposable, ILogger logger = null)
        {
            try
            {
                if (disposable == null) return;

                if (logger?.IsEnabled(LogLevel.Debug) ?? false) logger.LogDebug($"Disposing {disposable.GetType()}");

                disposable.Dispose();

                if (logger?.IsEnabled(LogLevel.Debug) ?? false) logger.LogDebug($"Disposed {disposable.GetType()}");
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, "Disposal error");
            }
        }
    }
}
