using System;
using System.Threading.Tasks;
using Loco.Core.Caching;

namespace Loco.Core.Services
{
    /// <summary>
    /// Simple wrapper around FastCache for backward compatibility
    /// Following John Carmack's performance principles
    /// </summary>
    public sealed class CacheService : IDisposable
    {
        private readonly FastCache<string, object> _cache = new();

        public async Task<T> GetOrAddAsync<T>(string key, Func<Task<T>> factory, TimeSpan? expiration = null)
        {
            return await _cache.GetOrCreateAsync(key, async () => (object)await factory(), expiration).ContinueWith(t => (T)t.Result);
        }

        public bool TryGet<T>(string key, out T value)
        {
            if (_cache.TryGet(key, out object obj) && obj is T result)
            {
                value = result;
                return true;
            }
            value = default;
            return false;
        }

        public void Set<T>(string key, T value, TimeSpan? expiration = null)
        {
            _cache.Set(key, (object)value, expiration);
        }

        public bool Remove(string key)
        {
            return _cache.Remove(key);
        }

        public void Clear()
        {
            _cache.Clear();
        }

        public void Dispose()
        {
            _cache?.Dispose();
        }
    }
}