using RabbitMQ.Client;
using System;

using MassiveJobs.Core;

namespace MassiveJobs.RabbitMqBroker
{
    public static class ConnectionExtensions
    {
        public static void SafeClose(this IConnection connection, ILogger logger = null)
        {
            try
            {
                if (connection == null || !connection.IsOpen) return;
                connection.Close();
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, "Failed closing RabbitMQ connection");
            }
        }

        public static void SafeClose(this IModel model, ILogger logger = null)
        {
            try
            {
                if (model == null || model.IsClosed) return;
                model.Close();
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, "Failed closing RabbitMQ model");
            }
        }
    }
}
