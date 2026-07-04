using System.Security.Claims;
using Microsoft.Extensions.Caching.Memory;

namespace DonBot.Api.Services;

public sealed record AccessibleGuildsCacheResult<T>(bool IsUnauthorized, IReadOnlyList<T> Guilds);

public sealed class AccessibleGuildsCache(
    IUserGuildsService userGuilds,
    IMemoryCache cache)
{
    public async Task<AccessibleGuildsCacheResult<T>> GetAsync<T>(
        ClaimsPrincipal user,
        string cacheKeyPrefix,
        TimeSpan successTtl,
        TimeSpan errorTtl,
        Func<IReadOnlyList<DiscordUserGuild>, CancellationToken, Task<IReadOnlyList<T>>> buildResult,
        CancellationToken ct = default)
    {
        var discordId = user.FindFirst("discord_id")?.Value;
        if (string.IsNullOrEmpty(discordId))
        {
            return new AccessibleGuildsCacheResult<T>(true, []);
        }

        var cacheKey = $"{cacheKeyPrefix}:{discordId}";
        var guilds = await cache.GetOrCoalesceAsync(cacheKey, successTtl, errorTtl, async () =>
        {
            var userGuildList = await userGuilds.GetForPrincipalAsync(user, CancellationToken.None);
            return userGuildList is null
                ? []
                : await buildResult(userGuildList, CancellationToken.None);
        });

        return new AccessibleGuildsCacheResult<T>(false, guilds);
    }
}
