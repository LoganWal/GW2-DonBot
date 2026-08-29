using System.Collections.Concurrent;
using Discord;

namespace DonBot.Api.Services;

public interface IDiscordGuildMembershipService
{
    Task<IReadOnlySet<long>> GetMemberGuildIdsAsync(
        long discordId,
        IReadOnlyCollection<long> guildIds,
        CancellationToken ct = default);
}

public sealed class DiscordGuildMembershipService(
    DiscordRestClientProvider clientProvider,
    ILogger<DiscordGuildMembershipService> logger) : IDiscordGuildMembershipService
{
    internal const int MaxConcurrentLookups = 8;

    public async Task<IReadOnlySet<long>> GetMemberGuildIdsAsync(
        long discordId,
        IReadOnlyCollection<long> guildIds,
        CancellationToken ct = default)
    {
        if (discordId <= 0 || guildIds.Count == 0)
        {
            return new HashSet<long>();
        }

        var userId = (ulong)discordId;
        var client = await clientProvider.GetClientAsync();
        return await ResolveMembershipsAsync(
            discordId,
            guildIds,
            async (guildId, token) =>
            {
                var options = new RequestOptions { CancelToken = token };
                var guild = await client.GetGuildAsync((ulong)guildId, options);
                return guild is not null && await guild.GetUserAsync(userId, options) is not null;
            },
            logger,
            ct);
    }

    internal static async Task<IReadOnlySet<long>> ResolveMembershipsAsync(
        long discordId,
        IReadOnlyCollection<long> guildIds,
        Func<long, CancellationToken, Task<bool>> isMemberAsync,
        ILogger logger,
        CancellationToken ct)
    {
        if (discordId <= 0 || guildIds.Count == 0)
        {
            return new HashSet<long>();
        }

        var memberGuildIds = new ConcurrentDictionary<long, byte>();
        await Parallel.ForEachAsync(
            guildIds.Where(guildId => guildId > 0).Distinct(),
            new ParallelOptions
            {
                CancellationToken = ct,
                MaxDegreeOfParallelism = MaxConcurrentLookups
            },
            async (guildId, token) =>
            {
                try
                {
                    if (await isMemberAsync(guildId, token))
                    {
                        memberGuildIds.TryAdd(guildId, 0);
                    }
                }
                catch (OperationCanceledException) when (token.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    logger.LogWarning(
                        ex,
                        "Failed to resolve Discord membership for user {DiscordId} in guild {GuildId}.",
                        discordId,
                        guildId);
                }
            });

        return memberGuildIds.Keys.ToHashSet();
    }
}
