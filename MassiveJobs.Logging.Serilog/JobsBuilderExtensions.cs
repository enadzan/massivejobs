using MassiveJobs.Core;

namespace MassiveJobs.Logging.Serilog
{
    public static class JobsBuilderExtensions
    {
        public static JobsBuilder WithSerilog(this JobsBuilder builder)
        {
            builder.RegisterSingleton<IJobLoggerFactory, LoggerWrapperFactory>();
            return builder;
        }
    }
}
