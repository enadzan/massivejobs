using MassiveJobs.Core;

namespace MassiveJobs.RabbitMqBroker
{
    public class RabbitMqSettings
    {
        public string[] HostNames { get; set; } = new string[0];
        public int Port { get; set; } = -1;
        public string VirtualHost { get; set; } = "/";
        public string Username { get; set; }
        public string Password { get; set; }

        public bool SslEnabled { get; set; }
        public string SslServerName { get; set; }
        public string SslClientCertPath { get; set; }
        public string SslClientCertPassphrase { get; set; }

        /// <summary>
        /// Prefix to be appended to the exchange name and all the queue names.
        /// </summary>
        public string NamePrefix { get; set; }
    }
}
