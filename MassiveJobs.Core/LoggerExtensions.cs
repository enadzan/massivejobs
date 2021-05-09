using System;

namespace MassiveJobs.Core
{
    public static class LoggerExtensions
    {
        public static IJobLogger<TCategory> CreateLogger<TCategory>(this IJobLoggerFactory loggerFactory)
        {
            return new DefaultJobLogger<TCategory>(loggerFactory);
        }

        public static void LogTrace(this IJobLogger logger, string message)
        {
            logger.Log(JobLogLevel.Trace, null, message);
        }

        public static void LogDebug(this IJobLogger logger, string message)
        {
            logger.Log(JobLogLevel.Debug, null, message);
        }

        public static void LogDebug(this IJobLogger logger, Exception ex, string message)
        {
            logger.Log(JobLogLevel.Debug, ex, message);
        }

        public static void LogInformation(this IJobLogger logger, string message)
        {
            logger.Log(JobLogLevel.Information, null, message);
        }

        public static void LogWarning(this IJobLogger logger, string message)
        {
            logger.Log(JobLogLevel.Warning, null, message);
        }

        public static void LogWarning(this IJobLogger logger, Exception exception, string message)
        {
            logger.Log(JobLogLevel.Warning, exception, message);
        }

        public static void LogError(this IJobLogger logger, string message)
        {
            logger.Log(JobLogLevel.Error, null, message);
        }

        public static void LogError(this IJobLogger logger, Exception exception, string message)
        {
            logger.Log(JobLogLevel.Error, exception, message);
        }

        public static void LogCritical(this IJobLogger logger, string message)
        {
            logger.Log(JobLogLevel.Critical, null, message);
        }

        public static void LogCritical(this IJobLogger logger, Exception exception, string message)
        {
            logger.Log(JobLogLevel.Critical, exception, message);
        }
    }
}
