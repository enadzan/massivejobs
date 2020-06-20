using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace MassiveJobs.Core.Memory
{
    class InMemoryMessages
    {
        private ulong _lastDeliveryTag = 0;

        private readonly Dictionary<string, Tuple<List<RawMessage>, AutoResetEvent>> _publishedMessages
                    = new Dictionary<string, Tuple<List<RawMessage>, AutoResetEvent>>();

        public void EnsureQueue(string queueName)
        {
            lock (_publishedMessages)
            {
                if (!_publishedMessages.TryGetValue(queueName, out var messages))
                {
                    messages = new Tuple<List<RawMessage>, AutoResetEvent>(new List<RawMessage>(), new AutoResetEvent(false));
                    _publishedMessages.Add(queueName, messages);
                }
            }
        }

        public void EnqueueMessage(string routingKey, RawMessage rawMessage, int maxQueueLength)
        {
            lock (_publishedMessages)
            {
                if (!_publishedMessages.TryGetValue(routingKey, out var messages)) throw new Exception($"Unroutable message (routingKey={routingKey})");
                if (maxQueueLength > 0 && messages.Item1.Count >= maxQueueLength) throw new Exception($"Queue {routingKey} is full!");

                rawMessage.DeliveryTag = ++_lastDeliveryTag;

                messages.Item1.Add(rawMessage);
                messages.Item2.Set();
            }
        }

        public bool GetMessages(string queueName, ulong lastReceivedTag, out List<RawMessage> batch)
        {
            batch = null;
            AutoResetEvent waitSignal;

            lock (_publishedMessages)
            {
                if (!_publishedMessages.TryGetValue(queueName, out var messages)) throw new Exception($"Unknown queue ({queueName})");

                waitSignal = messages.Item2;
            }

            if (!waitSignal.WaitOne(1000))
            {
                return false;
            }

            lock (_publishedMessages)
            {
                if (!_publishedMessages.TryGetValue(queueName, out var messages)) return false;

                batch = new List<RawMessage>();

                foreach (var message in messages.Item1.Where(m => m.DeliveryTag > lastReceivedTag))
                {
                    batch.Add(message);
                }
            }

            return true;
        }

        public int GetCount(string queueName)
        {
            lock (_publishedMessages)
            {
                if (!_publishedMessages.TryGetValue(queueName, out var messages)) throw new Exception($"Unknown queue ({queueName})");
                return messages.Item1.Count;
            }
        }

        public void RemoveBatch(string queueName, ulong lastDeliveryTag)
        {
            lock (_publishedMessages)
            {
                if (!_publishedMessages.TryGetValue(queueName, out var messages)) throw new Exception($"Unknown queue ({queueName})");

                while (messages.Item1.Count > 0 && messages.Item1[0].DeliveryTag <= lastDeliveryTag)
                {
                    messages.Item1.RemoveAt(0);
                }
            }
        }

        public void RemoveMessage(string queueName, ulong deliveryTag)
        {
            lock (_publishedMessages)
            {
                if (!_publishedMessages.TryGetValue(queueName, out var messages)) throw new Exception($"Unknown queue ({queueName})");

                messages.Item1.RemoveAll(m => m.DeliveryTag == deliveryTag);
            }
        }
    }
}
