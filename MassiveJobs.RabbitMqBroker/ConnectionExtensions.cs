using RabbitMQ.Client;
using System;

using Microsoft.Extensions.Logging;
using System.Threading.Tasks;

namespace MassiveJobs.RabbitMqBroker
{
    public static class ConnectionExtensions
    {
        public static async Task SafeCloseAsync(this IConnection connection, ILogger logger = null)
        {
            try
            {
                if (connection == null || !connection.IsOpen) return;

                await connection.CloseAsync()
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, "Failed closing RabbitMQ connection");
            }
        }

        public static async Task SafeCloseAsync(this IChannel channel, ILogger logger = null)
        {
            try
            {
                if (channel == null || channel.IsClosed) return;

                await channel.CloseAsync()
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, "Failed closing RabbitMQ model");
            }
        }
    }
}
