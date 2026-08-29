using DonBot.Api.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace DonBot.Tests.Services.Api;

public class DiscordGuildMembershipServiceTests
{
    [Fact]
    public async Task ResolveMembershipsAsync_RunsIndependentLookupsWithBoundedConcurrency()
    {
        var guildIds = Enumerable.Range(1, 16).Select(id => (long)id).ToArray();
        var overlapReached = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var active = 0;
        var maxActive = 0;
        var concurrencyLock = new object();

        async Task<bool> IsMemberAsync(long guildId, CancellationToken ct)
        {
            lock (concurrencyLock)
            {
                active++;
                maxActive = Math.Max(maxActive, active);
                if (active >= 2)
                {
                    overlapReached.TrySetResult(true);
                }
            }

            try
            {
                await overlapReached.Task.WaitAsync(TimeSpan.FromSeconds(2), ct);
                await Task.Delay(20, ct);
                return true;
            }
            finally
            {
                lock (concurrencyLock)
                {
                    active--;
                }
            }
        }

        var result = await DiscordGuildMembershipService.ResolveMembershipsAsync(
            123,
            guildIds,
            IsMemberAsync,
            NullLogger.Instance,
            CancellationToken.None);

        Assert.Equal(guildIds.Order(), result.Order());
        Assert.InRange(maxActive, 2, DiscordGuildMembershipService.MaxConcurrentLookups);
    }

    [Fact]
    public async Task ResolveMembershipsAsync_RequestCancellationStopsLookups()
    {
        using var cancellation = new CancellationTokenSource();

        async Task<bool> IsMemberAsync(long guildId, CancellationToken ct)
        {
            cancellation.Cancel();
            await Task.Delay(Timeout.InfiniteTimeSpan, ct);
            return true;
        }

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            DiscordGuildMembershipService.ResolveMembershipsAsync(
                123,
                [1, 2, 3],
                IsMemberAsync,
                NullLogger.Instance,
                cancellation.Token));
    }
}
