using Discord.Rest;
using Microsoft.Extensions.Caching.Memory;

namespace DonBot.Api.Services;

public interface ILiveRaidMembership
{
    Task<bool> IsMemberAsync(ulong discordId, long guildId, CancellationToken ct = default);
    Task<HashSet<long>> FilterMemberGuildsAsync(ulong discordId, IReadOnlyCollection<long> candidateGuildIds, CancellationToken ct = default);
}

// Membership cache keyed per (user, guild). Checks fan out only over the
// supplied candidate guilds, not every guild the bot is in, so cold latency
// scales with candidate count rather than total bot guild count. The
// bot-guild dictionary is cached for 60s.
public sealed class LiveRaidMembership(
    DiscordRestClientProvider clientProvider,
    IMemoryCache cache,
    ILogger<LiveRaidMembership> logger) : ILiveRaidMembership
{
    private static readonly TimeSpan PositiveTtl = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan ErrorTtl = TimeSpan.FromSeconds(10);

    public Task<bool> IsMemberAsync(ulong discordId, long guildId, CancellationToken ct = default)
    {
        return CheckMembershipAsync(discordId, guildId);
    }

    public async Task<HashSet<long>> FilterMemberGuildsAsync(ulong discordId, IReadOnlyCollection<long> candidateGuildIds, CancellationToken ct = default)
    {
        if (candidateGuildIds.Count == 0)
        {
            return new HashSet<long>();
        }
        var tasks = candidateGuildIds
            .Select(async gid => (Id: gid, IsMember: await CheckMembershipAsync(discordId, gid)))
            .ToList();
        var results = await Task.WhenAll(tasks);
        return results.Where(r => r.IsMember).Select(r => r.Id).ToHashSet();
    }

    private Task<bool> CheckMembershipAsync(ulong discordId, long guildId)
    {
        var key = $"live-raid:member:{discordId}:{guildId}";
        return cache.GetOrCoalesceAsync(key, PositiveTtl, ErrorTtl, async () =>
        {
            try
            {
                var botGuilds = await GetBotGuildsAsync();
                if (!botGuilds.TryGetValue(guildId, out var botGuild))
                {
                    return false;
                }
                return await botGuild.GetUserAsync(discordId) != null;
            }
            catch (Exception ex)
            {
                logger.LogDebug(ex, "Membership check failed for user {UserId} guild {GuildId}.", discordId, guildId);
                return false;
            }
        });
    }

    private Task<Dictionary<long, RestGuild>> GetBotGuildsAsync()
    {
        return cache.GetOrCoalesceAsync("live-raid:bot-guilds", PositiveTtl, ErrorTtl, async () =>
        {
            var client = await clientProvider.GetClientAsync();
            var botGuilds = await client.GetGuildsAsync();
            return botGuilds.ToDictionary(g => (long)g.Id, g => g);
        });
    }
}
