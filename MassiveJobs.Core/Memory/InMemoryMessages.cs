using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace MassiveJobs.Core.Memory
{
    public class InMemoryMessages: IDisposable
    {
        private ulong _lastDeliveryTag;

        private readonly Dictionary<string, InMemoryQueue> _queues = new Dictionary<string, InMemoryQueue>();

        internal void EnsureQueues(MassiveJobsSettings settings)
        {
            lock (_queues)
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
            lock (_queues)
            {
                if (!_queues.TryGetValue(routingKey, out var queue)) throw new Exception($"Unroutable message (routingKey={routingKey})");
                if (maxQueueLength > 0 && queue.MessagesReady.Count >= maxQueueLength) throw new Exception($"Queue {routingKey} is full!");

                rawMessage.DeliveryTag = ++_lastDeliveryTag;

                queue.MessagesReady.Add(rawMessage.DeliveryTag, rawMessage);
                queue.MessageReceivedEvent.Set();
            }
        }

        public bool GetMessages(string queueName, out List<RawMessage> batch)
        {
            batch = null;
            AutoResetEvent waitSignal;

            lock (_queues)
            {
                if (!_queues.TryGetValue(queueName, out var queue)) throw new Exception($"Unknown queue ({queueName})");

                waitSignal = queue.MessageReceivedEvent;

                if (queue.MessagesReady.Count > 0)
                {
                    waitSignal.Set();
                }
            }

            if (!waitSignal.WaitOne(100))
            {
                return false;
            }

            lock (_queues)
            {
                if (!_queues.TryGetValue(queueName, out var queue)) return false;

                batch = new List<RawMessage>();

                while (queue.MessagesReady.Count > 0)
                {
                    var kvp = queue.MessagesReady.First();

                    batch.Add(kvp.Value);

                    queue.MessagesUnacknowledged.Add(kvp.Key, kvp.Value);
                    queue.MessagesReady.Remove(kvp.Key);
                }
            }

            return true;
        }

        public int GetCount(string queueName)
        {
            lock (_queues)
            {
                if (!_queues.TryGetValue(queueName, out var queue)) throw new Exception($"Unknown queue ({queueName})");
                return queue.MessagesReady.Count + queue.MessagesUnacknowledged.Count;
            }
        }

        public int GetCount()
        {
            lock (_queues)
            {
                return _queues.Values.Sum(q => q.MessagesReady.Count + q.MessagesUnacknowledged.Count);
            }
        }

        public void RemoveMessage(string queueName, ulong deliveryTag)
        {
            lock (_queues)
            {
                if (!_queues.TryGetValue(queueName, out var queue)) throw new Exception($"Unknown queue ({queueName})");

                queue.MessagesUnacknowledged.Remove(deliveryTag);
            }
        }

        public void MoveUnackToReady(string queueName)
        {
            lock (_queues)
            {
                if (!_queues.TryGetValue(queueName, out var queue)) throw new Exception($"Unknown queue ({queueName})");

                while (queue.MessagesUnacknowledged.Count > 0)
                {
                    var kvp = queue.MessagesUnacknowledged.First();

                    queue.MessagesReady.Add(kvp.Key, kvp.Value);
                    queue.MessagesUnacknowledged.Remove(kvp.Key);
                }
            }
        }

        private void EnsureQueue(string queueName)
        {
            if (!_queues.TryGetValue(queueName, out _))
            {
                _queues.Add(queueName, new InMemoryQueue());
            }
        }

        public void Dispose()
        {
            lock (_queues) 
            {
                foreach (var kvp in _queues)
                {
                    kvp.Value.Dispose();
                }

                _queues.Clear();
            }
        }

        private class InMemoryQueue: IDisposable
        {
            public Dictionary<ulong, RawMessage> MessagesReady { get; }

            public Dictionary<ulong, RawMessage> MessagesUnacknowledged { get; }

            public AutoResetEvent MessageReceivedEvent { get; }

            public InMemoryQueue()
            {
                MessagesReady = new Dictionary<ulong, RawMessage>();
                MessagesUnacknowledged = new Dictionary<ulong, RawMessage>();
                MessageReceivedEvent = new AutoResetEvent(false);
            }

            public void Dispose()
            {
                MessageReceivedEvent.Dispose();
            }
        }
    }
}
