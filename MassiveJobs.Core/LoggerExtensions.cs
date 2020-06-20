using System;

namespace MassiveJobs.Core
{
    public static class LoggerExtensions
    {
        public static void LogTrace(this ILogger logger, string message)
        {
            logger.Log(LogLevel.Trace, null, message);
        }

        public static void LogDebug(this ILogger logger, string message)
        {
            logger.Log(LogLevel.Debug, null, message);
        }

        public static void LogInformation(this ILogger logger, string message)
        {
            logger.Log(LogLevel.Information, null, message);
        }

        public static void LogWarning(this ILogger logger, string message)
        {
            logger.Log(LogLevel.Warning, null, message);
        }

        public static void LogWarning(this ILogger logger, Exception exception, string message)
        {
            logger.Log(LogLevel.Warning, exception, message);
        }

        public static void LogError(this ILogger logger, string message)
        {
            logger.Log(LogLevel.Error, null, message);
        }

        public static void LogError(this ILogger logger, Exception exception, string message)
        {
            logger.Log(LogLevel.Error, exception, message);
        }

        public static void LogCritical(this ILogger logger, string message)
        {
            logger.Log(LogLevel.Critical, null, message);
        }

        public static void LogCritical(this ILogger logger, Exception exception, string message)
        {
            logger.Log(LogLevel.Critical, exception, message);
        }
    }
}
