using MassiveJobs.Core;

namespace MassiveJobs.Logging.Log4Net
{
    public static class JobsBuilderExtensions
    {
        public static JobsBuilder WithLog4Net(this JobsBuilder builder)
        {
            builder.RegisterSingleton<IJobLoggerFactory, LoggerWrapperFactory>();
            return builder;
        }
    }
}
