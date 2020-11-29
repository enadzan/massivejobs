using MassiveJobs.Core;

namespace MassiveJobs.Logging.NLog
{
    public static class JobsBuilderExtensions
    {
        public static JobsBuilder WithNLog(this JobsBuilder builder)
        {
            builder.RegisterSingleton<IJobLoggerFactory, LoggerWrapperFactory>();
            return builder;
        }
    }
}
