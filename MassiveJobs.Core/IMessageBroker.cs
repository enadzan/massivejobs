using System;
using System.Collections.Generic;

using MassiveJobs.Core.DependencyInjection;

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
        void Publish(string rountingKey, IEnumerable<RawMessage> messages, TimeSpan timeout);
    }

    public interface IMessageConsumer: IDisposable
    {
        event MessageConsumerDisconnected Disconnected;
        
        void Connect();

        IMessageReceiver CreateReceiver(string queueName, bool singleActiveConsumer = false);
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
        /// <param name="lastDeliveryTag"></param>
        void AckBatchMessageProcessed(IJobServiceScope scope, ulong deliveryTag);

        /// <summary>
        /// This must me implemented to confirm the processing of a single message.
        /// </summary>
        /// <param name="scope"></param>
        /// <param name="deliveryTag"></param>
        void AckMessageProcessed(IJobServiceScope scope, ulong deliveryTag);
    }
}
