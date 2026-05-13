using Microsoft.Extensions.Caching.Memory;

namespace DonBot.Api.Services;

public static class MemoryCacheCoalesceExtensions
{
    private static readonly object Gate = new();

    // Stores the in-flight Task<T> in the cache so concurrent misses share one
    // factory invocation. On failure the entry's TTL is shortened so callers
    // can retry without waiting for the long positive TTL.
    public static Task<T> GetOrCoalesceAsync<T>(
        this IMemoryCache cache,
        object key,
        TimeSpan ttl,
        TimeSpan errorTtl,
        Func<Task<T>> factory)
    {
        if (cache.TryGetValue<Task<T>>(key, out var existing) && existing is not null)
        {
            return existing;
        }

        lock (Gate)
        {
            if (cache.TryGetValue<Task<T>>(key, out existing) && existing is not null)
            {
                return existing;
            }

            var task = factory();
            cache.Set(key, task, ttl);
            _ = task.ContinueWith(t =>
            {
                if (t.IsFaulted || t.IsCanceled)
                {
                    cache.Set(key, task, errorTtl);
                }
            }, TaskScheduler.Default);
            return task;
        }
    }
}
