namespace MassiveJobs.RabbitMqBroker
{
    public class RabbitMqSettings
    {
        public string[] HostNames { get; set; } = { "localhost" };
        public int Port { get; set; } = -1;
        public string VirtualHost { get; set; } = "/";
        public string Username { get; set; } = "guest";
        public string Password { get; set; } = "guest";

        public bool SslEnabled { get; set; }
        public string SslServerName { get; set; }
        public string SslClientCertPath { get; set; }
        public string SslClientCertPassPhrase { get; set; }

        public ushort PrefetchCount { get; set; } = 100;
    }
}
