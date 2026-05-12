using Discord.Rest;
using Microsoft.Extensions.Caching.Memory;

namespace DonBot.Api.Services;

public interface ILiveRaidMembership
{
    Task<bool> IsMemberAsync(ulong discordId, long guildId, CancellationToken ct = default);
    Task<HashSet<long>> FilterMemberGuildsAsync(ulong discordId, IReadOnlyCollection<long> candidateGuildIds, CancellationToken ct = default);
    Task<HashSet<long>> GetUserMemberGuildsAsync(ulong discordId, CancellationToken ct = default);
}

// Membership lookup with two caches:
//   live-raid:bot-guilds        -> Dictionary<long, RestGuild> of every guild the bot is in
//   live-raid:user-guilds:{uid} -> HashSet<long> of guilds where uid is a member
// Both have 60s TTL. The user-guild set is computed once by checking every bot guild
// in parallel, so subsequent ListGuilds / per-guild authorize calls just hit cache.
public sealed class LiveRaidMembership(
    DiscordRestClientProvider clientProvider,
    IMemoryCache cache,
    ILogger<LiveRaidMembership> logger) : ILiveRaidMembership
{
    private static readonly TimeSpan PositiveTtl = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan ErrorTtl = TimeSpan.FromSeconds(10);

    public async Task<bool> IsMemberAsync(ulong discordId, long guildId, CancellationToken ct = default)
    {
        var memberGuilds = await GetUserMemberGuildsAsync(discordId, ct);
        return memberGuilds.Contains(guildId);
    }

    public async Task<HashSet<long>> FilterMemberGuildsAsync(ulong discordId, IReadOnlyCollection<long> candidateGuildIds, CancellationToken ct = default)
    {
        if (candidateGuildIds.Count == 0)
        {
            return new HashSet<long>();
        }
        var memberGuilds = await GetUserMemberGuildsAsync(discordId, ct);
        return candidateGuildIds.Where(memberGuilds.Contains).ToHashSet();
    }

    public async Task<HashSet<long>> GetUserMemberGuildsAsync(ulong discordId, CancellationToken ct = default)
    {
        var cacheKey = $"live-raid:user-guilds:{discordId}";
        if (cache.TryGetValue<HashSet<long>>(cacheKey, out var cached) && cached != null)
        {
            return cached;
        }

        try
        {
            var botGuilds = await GetBotGuildsAsync();

            // Fan out: ask Discord whether the user is in each bot guild concurrently.
            // Discord.Net's REST stack handles per-route rate limiting internally, so
            // we just dispatch them all and let it queue as needed.
            var tasks = botGuilds.Values
                .Select(async botGuild => (Id: (long)botGuild.Id, IsMember: await SafeIsMemberAsync(botGuild, discordId)))
                .ToList();
            var results = await Task.WhenAll(tasks);

            var set = results.Where(r => r.IsMember).Select(r => r.Id).ToHashSet();
            cache.Set(cacheKey, set, PositiveTtl);
            return set;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to compute member guild set for user {UserId}.", discordId);
            var empty = new HashSet<long>();
            cache.Set(cacheKey, empty, ErrorTtl);
            return empty;
        }
    }

    private async Task<Dictionary<long, RestGuild>> GetBotGuildsAsync()
    {
        const string cacheKey = "live-raid:bot-guilds";
        if (cache.TryGetValue<Dictionary<long, RestGuild>>(cacheKey, out var cached) && cached != null)
        {
            return cached;
        }
        var client = await clientProvider.GetClientAsync();
        var botGuilds = await client.GetGuildsAsync();
        var dict = botGuilds.ToDictionary(g => (long)g.Id, g => g);
        cache.Set(cacheKey, dict, PositiveTtl);
        return dict;
    }

    private async Task<bool> SafeIsMemberAsync(RestGuild guild, ulong userId)
    {
        try
        {
            return await guild.GetUserAsync(userId) != null;
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "GetUserAsync threw for user {UserId} in guild {GuildId}.", userId, guild.Id);
            return false;
        }
    }
}
