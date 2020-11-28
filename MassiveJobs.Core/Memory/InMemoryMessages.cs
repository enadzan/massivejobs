using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace MassiveJobs.Core.Memory
{
    public class InMemoryMessages
    {
        private ulong _lastDeliveryTag;

        private readonly Dictionary<string, Tuple<List<RawMessage>, AutoResetEvent>> _publishedMessages
                    = new Dictionary<string, Tuple<List<RawMessage>, AutoResetEvent>>();

        internal void EnsureQueues(MassiveJobsSettings settings)
        {
            lock (_publishedMessages)
            {
                for (var i = 0; i < settings.ImmediateWorkersCount; i++)
                {
                    EnsureQueue(string.Format(settings.ImmediateQueueNameTemplate, i));
                }

                for (var i = 0; i < settings.ScheduledWorkersCount; i++)
                {
                    EnsureQueue(string.Format(settings.ScheduledQueueNameTemplate, i));
                }

                for (var i = 0; i < settings.PeriodicWorkersCount; i++)
                {
                    EnsureQueue(string.Format(settings.PeriodicQueueNameTemplate, i));
                }

                for (var i = 0; i < settings.LongRunningWorkersCount; i++)
                {
                    EnsureQueue(string.Format(settings.LongRunningQueueNameTemplate, i));
                }

                EnsureQueue(settings.ErrorQueueName);
                EnsureQueue(settings.FailedQueueName);
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

                if (messages.Item1.Count > 0)
                {
                    waitSignal.Set();
                }
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

        private void EnsureQueue(string queueName)
        {
            if (!_publishedMessages.TryGetValue(queueName, out var messages))
            {
                messages = new Tuple<List<RawMessage>, AutoResetEvent>(new List<RawMessage>(), new AutoResetEvent(false));
                _publishedMessages.Add(queueName, messages);
            }
        }
    }
}
