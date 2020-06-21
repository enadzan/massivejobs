using System;
using System.Collections.Generic;

namespace MassiveJobs.Core
{
    public class PublishersPool: IDisposable
    {
        private readonly IMessageBroker _messageBroker;
        private readonly int _maxRetained;

        private readonly Queue<IMessagePublisher> _publishers;

        public PublishersPool(IMessageBroker messageBroker, int maxRetained)
        {
            _messageBroker = messageBroker;
            _maxRetained = maxRetained;
            _publishers = new Queue<IMessagePublisher>();
        }

        public IMessagePublisher Get()
        {
            lock (_publishers)
            {
                if (_publishers.Count > 0) return _publishers.Dequeue();
            }

            return _messageBroker.CreatePublisher();
        }

        public void Return(IMessagePublisher publisher)
        {
            lock (_publishers)
            {
                if (_publishers.Count < _maxRetained)
                {
                    _publishers.Enqueue(publisher);
                    return;
                }
            }

            publisher.SafeDispose();
        }

        public bool AllOk()
        {
            lock (_publishers)
            {
                foreach (var publisher in _publishers)
                {
                    if (!publisher.IsOk) return false;
                }
            }

            return true;
        }

        public void Dispose()
        {
            lock (_publishers)
            {
                while (_publishers.Count > 0)
                {
                    _publishers
                        .Dequeue()
                        .SafeDispose();
                }
            }
        }
    }
}
