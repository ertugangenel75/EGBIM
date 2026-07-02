using System;
using System.Collections.Concurrent;

namespace EGBIMOTO.Core.Cache
{
    /// <summary>
    /// In-memory step sonucu cache.
    /// ManifestRunner / DagExecutor aynı op+params kombinasyonunu tekrar çalıştırmaz.
    /// TTL: varsayılan 30 dakika (Revit session boyunca stale data önlenir).
    ///
    /// v3.2: Dictionary → ConcurrentDictionary ile thread-safe hale getirildi.
    ///   TryGet / Set imzası basitleştirildi: artık object? saklar.
    ///   DagExecutor ParallelExecutionEnabled=true ile birden fazla thread
    ///   aynı anda cache'e yazabilir — artık güvenli.
    /// </summary>
    public sealed class NodeCacheService
    {
        private readonly TimeSpan _ttl;

        private sealed class Entry
        {
            public object?  Output    { get; init; }
            public DateTime ExpiresAt { get; init; }
        }

        // ConcurrentDictionary — thread-safe okuma/yazma
        private readonly ConcurrentDictionary<string, Entry> _store = new();

        public NodeCacheService(TimeSpan? ttl = null)
            => _ttl = ttl ?? TimeSpan.FromMinutes(30);

        public bool TryGet(string key, out object? output)
        {
            if (_store.TryGetValue(key, out var entry) && DateTime.UtcNow < entry.ExpiresAt)
            {
                output = entry.Output;
                return true;
            }
            _store.TryRemove(key, out _);
            output = null;
            return false;
        }

        public void Set(string key, object? output)
            => _store[key] = new Entry { Output = output, ExpiresAt = DateTime.UtcNow.Add(_ttl) };

        public void Invalidate(string key) => _store.TryRemove(key, out _);

        public void Clear() => _store.Clear();

        public int Count => _store.Count;
    }
}
