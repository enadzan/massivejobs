namespace MassiveJobs.RabbitMqBroker
{
    public class RabbitMqSettings
    {
        public string[] HostNames { get; set; } = new[] { "localhost" };
        public int Port { get; set; } = -1;
        public string VirtualHost { get; set; } = "/";
        public string Username { get; set; } = "guest";
        public string Password { get; set; } = "guest";

        public bool SslEnabled { get; set; }
        public string SslServerName { get; set; }
        public string SslClientCertPath { get; set; }
        public string SslClientCertPassphrase { get; set; }

        public string ExchangeName { get; private set; } = Constants.ExchangeName;

        public ushort PrefetchCount { get; set; } = 100;

        /// <summary>
        /// Prefix to be appended to the exchange name and all the queue names.
        /// </summary>
        public string NamePrefix 
        {
            get
            {
                return _namePrefix;
            }
            set
            {
                _namePrefix = value;
                OnNamePrefixChanged();
            }
        }

        protected virtual void OnNamePrefixChanged()
        {
            ExchangeName = $"{_namePrefix}{Constants.ExchangeName}";
        }

        private string _namePrefix = "";
    }
}
