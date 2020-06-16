using RabbitMQ.Client;

namespace MassiveJobs.RabbitMqBroker
{
    static class ConnectionExtensions
    {
        public static void SafeClose(this IConnection connection)
        {
            try
            {
                connection?.Close();
            }
            catch
            {
            }
        }

        public static void SafeClose(this IModel model)
        {
            try
            {
                model?.Close();
            }
            catch
            {
            }
        }
    }
}
