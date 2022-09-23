using System.Runtime.Caching;

namespace Services.CacheServices
{
    internal class CacheService : ICacheService
    {
        private readonly MemoryCache cache = MemoryCache.Default;

        public void Set<T>(string key, T value) where T: notnull
        {
            cache.Set(key, value, null);
        }

        public T? Get<T>(string key) where T: class
        {
            return cache.Get(key) as T;
        }
    }
}
