using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;

namespace MassiveJobs.Core
{
    public delegate void MessageConsumerDisconnected(IMessageConsumer sender);
    public delegate void MessageReceivedHandler(IMessageReceiver sender, RawMessage message);

    public class RawMessage
    {
        public string TypeTag;
        public byte[] Body;
        public bool IsPersistent;
        public ulong DeliveryTag;
    }

    public interface IMessagePublisher: IDisposable
    {
        void Publish(string routingKey, IEnumerable<RawMessage> messages, TimeSpan timeout);
    }

    public interface IMessageConsumer: IDisposable
    {
        event MessageConsumerDisconnected Disconnected;
        
        void Connect();

        IMessageReceiver CreateReceiver(string queueName);
    }

    public interface IMessageReceiver: IDisposable
    {
        event MessageReceivedHandler MessageReceived;

        void Start();

        /// <summary>
        /// Implementations can choose to acknowledge the entire batch OR each message in the batch.
        /// Acknowledging each message in the batch allows to do the acknowledgment in the same scope used for message processing.
        /// In other words implement <see cref="AckBatchProcessed(ulong)"/> or <see cref="AckBatchMessageProcessed(IJobServiceScope, ulong)"/>.
        /// No need to implement both. However, <see cref="AckMessageProcessed(IJobServiceScope, ulong)"/> always has to be implemented.
        /// </summary>
        /// <param name="lastDeliveryTag"></param>
        void AckBatchProcessed(ulong lastDeliveryTag);

        /// <summary>
        /// Implementations can choose to acknowledge the entire batch OR each message in the batch.
        /// Acknowledging each message in the batch allows to do the acknowledgment in the same scope used for message processing.
        /// In other words implement <see cref="AckBatchProcessed(ulong)"/> or <see cref="AckBatchMessageProcessed(IJobServiceScope, ulong)"/>.
        /// No need to implement both. However, <see cref="AckMessageProcessed(IJobServiceScope, ulong)"/> always has to be implemented.
        /// </summary>
        /// <param name="scope">Service scope in which a message from a batch is processed.</param>
        /// <param name="deliveryTag">Message delivery tag</param>
        void AckBatchMessageProcessed(IServiceScope scope, ulong deliveryTag);

        /// <summary>
        /// This must me implemented to confirm the processing of a single message.
        /// </summary>
        /// <param name="scope"></param>
        /// <param name="deliveryTag"></param>
        void AckMessageProcessed(IServiceScope scope, ulong deliveryTag);

        IBrokerTransaction BeginTransaction(IServiceScope scope);
    }

    public interface IBrokerTransaction: IDisposable
    {
        void Commit();
        void Rollback();
    }
}
