using System;
using System.Linq;

namespace MassiveJobs.Core
{
    public static class ExceptionExtensions
    {
        public static string GetSummary(this Exception ex)
        {
            if (ex is AggregateException aggEx)
            {
                ex = aggEx.Flatten().InnerExceptions.FirstOrDefault();
            }

            if (ex == null) return "unknown_error";

            while (ex.InnerException != null)
            {
                ex = ex.InnerException;
            }

            var stackTraceLines = ex.StackTrace
                .Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries)
                .Take(10);

            return stackTraceLines.Aggregate(ex.Message, (acc, line) => acc + Environment.NewLine + line);
        }
    }
}
