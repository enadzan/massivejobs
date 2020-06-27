using System;

namespace MassiveJobs.Core
{
    public delegate void MessageConsumerDisconnected(IMessageConsumer sender);
    public delegate void MessageReceivedHandler(IMessageReceiver sender, RawMessage message);

    public class RawMessage
    {
        public string TypeTag;
        public byte[] Body;
        public ulong DeliveryTag;
    }

    public interface IMessagePublisher: IDisposable
    { 
        void Publish(string routingKey, ReadOnlyMemory<byte> body, string typeTag, bool persistent);
        void WaitForConfirmsOrDie(TimeSpan timeout);
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
        void AckBatchProcessed(ulong lastDeliveryTag);
        void AckMessageProcessed(ulong deliveryTag);
    }
}
