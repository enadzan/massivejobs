using System;

namespace MassiveJobs.Core
{
    public enum MessageBrokerType { JobPublisher = 0, WorkerCoordinator = 1 };

    public interface IMessageBrokerFactory
    {
        IMessageBroker CreateMessageBroker(MessageBrokerType brokerType);
    }

    public delegate void MessageBrokerDisconnectedHandler(IMessageBroker sender);

    public interface IMessageBroker: IDisposable
    {
        event MessageBrokerDisconnectedHandler Disconnected;

        IMessageConsumer CreateConsumer(string queueName);
        IMessagePublisher CreatePublisher();

        void DeclareTopology();
    }

    public delegate void MessageReceivedHandler(IMessageConsumer sender, RawMessage message);

    public interface IMessageConsumer: IDisposable
    {
        event MessageReceivedHandler MessageReceived;

        void AckBatchProcessed(ulong lastDeliveryTag);
        void AckMessageProcessed(ulong deliveryTag);
    }

    public interface IMessagePublisher: IDisposable
    {
        bool IsOk { get; }
        void Publish(string routingKey, ReadOnlyMemory<byte> body, string typeTag, bool persistent);
        void WaitForConfirmsOrDie(TimeSpan timeout);
    }

    public class RawMessage
    {
        public string TypeTag;
        public byte[] Body;
        public ulong DeliveryTag;
    }
}
