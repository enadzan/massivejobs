using Microsoft.Extensions.Logging;

namespace MassiveJobs.ConsoleTest
{
    class LoggerFactoryWrapper : Core.ILoggerFactory
    {
        private readonly ILoggerFactory _msLoggerFactory;

        public LoggerFactoryWrapper(ILoggerFactory msLoggerFactory)
        {
            _msLoggerFactory = msLoggerFactory;
        }

        public Core.ILogger<TCategory> CreateLogger<TCategory>()
        {
            return new LoggerWrapper<TCategory>(_msLoggerFactory.CreateLogger<TCategory>());
        }
    }
}
