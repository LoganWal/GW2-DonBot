using System.Runtime.Caching;

namespace DonBot.Services.CacheServices
{
    internal class CacheService : ICacheService
    {
        private readonly MemoryCache _cache = MemoryCache.Default;

        public void Set<T>(string key, T value, DateTimeOffset? expiry = null) where T: notnull
        {
            if (expiry == null)
            {
                _cache.Set(key, value, null);
            }
            else
            {
                _cache.Set(key, value, expiry.Value);
            }
        }

        public T? Get<T>(string key) where T: class
        {
            return _cache.Get(key) as T;
        }
    }
}
