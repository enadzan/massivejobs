using Microsoft.Extensions.Logging;

using MassiveJobs.Core;

namespace MassiveJobs.DotNetCore.ConsoleTest
{
    class LoggerFactoryWrapper : IJobLoggerFactory
    {
        private readonly ILoggerFactory _msLoggerFactory;

        public LoggerFactoryWrapper(ILoggerFactory msLoggerFactory)
        {
            _msLoggerFactory = msLoggerFactory;
        }

        public IJobLogger<TCategory> CreateLogger<TCategory>()
        {
            return new LoggerWrapper<TCategory>(_msLoggerFactory.CreateLogger<TCategory>());
        }
    }
}
