using DonBot.Api.Services;
using Microsoft.Extensions.Caching.Memory;

namespace DonBot.Tests.Services.Api;

public class MemoryCacheCoalesceExtensionsTests
{
    private static MemoryCache NewCache() => new(new MemoryCacheOptions());

    [Fact]
    public async Task ConcurrentCallers_ShareSingleFactoryInvocation()
    {
        using var cache = NewCache();
        var invocations = 0;
        var gate = new TaskCompletionSource<int>();

        Task<int> Factory()
        {
            Interlocked.Increment(ref invocations);
            return gate.Task;
        }

        var t1 = cache.GetOrCoalesceAsync("key", TimeSpan.FromSeconds(60), TimeSpan.FromSeconds(10), Factory);
        var t2 = cache.GetOrCoalesceAsync("key", TimeSpan.FromSeconds(60), TimeSpan.FromSeconds(10), Factory);
        var t3 = cache.GetOrCoalesceAsync("key", TimeSpan.FromSeconds(60), TimeSpan.FromSeconds(10), Factory);

        gate.SetResult(42);
        var results = await Task.WhenAll(t1, t2, t3);

        Assert.Equal(1, invocations);
        Assert.All(results, r => Assert.Equal(42, r));
    }

    [Fact]
    public async Task SubsequentCall_ReturnsCachedResult_WithoutReinvokingFactory()
    {
        using var cache = NewCache();
        var invocations = 0;

        Task<int> Factory()
        {
            Interlocked.Increment(ref invocations);
            return Task.FromResult(7);
        }

        var first = await cache.GetOrCoalesceAsync("key", TimeSpan.FromSeconds(60), TimeSpan.FromSeconds(10), Factory);
        var second = await cache.GetOrCoalesceAsync("key", TimeSpan.FromSeconds(60), TimeSpan.FromSeconds(10), Factory);

        Assert.Equal(7, first);
        Assert.Equal(7, second);
        Assert.Equal(1, invocations);
    }

    [Fact]
    public async Task DifferentKeys_DoNotShareFactoryInvocation()
    {
        using var cache = NewCache();
        var invocations = 0;

        Task<string> Factory(string label)
        {
            Interlocked.Increment(ref invocations);
            return Task.FromResult(label);
        }

        var a = await cache.GetOrCoalesceAsync("a", TimeSpan.FromSeconds(60), TimeSpan.FromSeconds(10), () => Factory("a"));
        var b = await cache.GetOrCoalesceAsync("b", TimeSpan.FromSeconds(60), TimeSpan.FromSeconds(10), () => Factory("b"));

        Assert.Equal("a", a);
        Assert.Equal("b", b);
        Assert.Equal(2, invocations);
    }

    [Fact]
    public async Task FailedTask_EvictedAfterErrorTtl_AllowsRetry()
    {
        using var cache = NewCache();
        var attempt = 0;

        Task<int> Factory()
        {
            attempt++;
            return attempt == 1
                ? Task.FromException<int>(new InvalidOperationException("boom"))
                : Task.FromResult(99);
        }

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            cache.GetOrCoalesceAsync("key", TimeSpan.FromSeconds(60), TimeSpan.FromMilliseconds(50), Factory));

        // Wait past errorTtl so the cached faulted task expires.
        await Task.Delay(150);

        var retry = await cache.GetOrCoalesceAsync("key", TimeSpan.FromSeconds(60), TimeSpan.FromMilliseconds(50), Factory);

        Assert.Equal(2, attempt);
        Assert.Equal(99, retry);
    }
}
