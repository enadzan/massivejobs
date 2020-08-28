using System;
using System.Collections.Concurrent;

namespace MassiveJobs.Core
{
    public class ScopePool: IDisposable
    {
        private readonly IJobServiceScopeFactory _serviceScopeFactory;
        private readonly ConcurrentBag<ScopePoolItem> _cache = new ConcurrentBag<ScopePoolItem>();

        public ScopePool(IJobServiceScopeFactory serviceScopeFactory)
        {
            _serviceScopeFactory = serviceScopeFactory;
        }

        public ScopePoolItem Get()
        {
            if (_cache.TryTake(out var item)) return item;

            return new ScopePoolItem(_serviceScopeFactory.CreateScope());
        }

        public void Return(ref ScopePoolItem item)
        {
            if (item == null || item.IsDisposed) return;

            if (++item.UsageCount < 100)
            {
                _cache.Add(item);
            }
            else
            {
                item.Dispose();
            }

            item = null;
        }

        public void Dispose()
        {
            while (_cache.TryTake(out var item)) item.Dispose();
        }
    }

    public class ScopePoolItem: IDisposable
    {
        public IJobServiceScope Scope { get; }
        public int UsageCount { get; set; }
        public bool IsDisposed { get; private set; }

        public ScopePoolItem(IJobServiceScope scope)
        {
            Scope = scope;
        }

        public void Dispose()
        {
            IsDisposed = true;
            Scope.Dispose();
        }
    }
}
