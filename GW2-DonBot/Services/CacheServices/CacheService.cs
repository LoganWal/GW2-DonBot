using System.Runtime.Caching;

namespace Services.CacheServices
{
    internal class CacheService : ICacheService
    {
        private readonly MemoryCache cache = MemoryCache.Default;

        public void Set<T>(string key, T value, DateTimeOffset? expiry = null) where T: notnull
        {
            if (expiry == null)
            {
                cache.Set(key, value, null);
            }
            else
            {
                cache.Set(key, value, expiry.Value);
            }
        }

        public T? Get<T>(string key) where T: class
        {
            return cache.Get(key) as T;
        }
    }
}
