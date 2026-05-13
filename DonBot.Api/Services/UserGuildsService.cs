using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;

namespace DonBot.Api.Services;

public record DiscordUserGuild(ulong Id, string Name, string? Icon, bool Owner, ulong Permissions);

public interface IUserGuildsService
{
    Task<IReadOnlyList<DiscordUserGuild>?> GetUserGuildsAsync(ulong discordId, string accessToken, CancellationToken ct = default);
    Task<IReadOnlyList<DiscordUserGuild>?> GetForPrincipalAsync(ClaimsPrincipal user, CancellationToken ct = default);
    Task<bool> IsMemberAsync(ClaimsPrincipal user, ulong guildId, CancellationToken ct = default);
    Task<bool> HasAdministratorAsync(ClaimsPrincipal user, ulong guildId, CancellationToken ct = default);
}

// Fetches the authenticated user's guild list from Discord via their OAuth
// token (requires the `guilds` scope). One API call per user vs N bot-side
// member-check calls, and rate limits are per-user instead of the bot's
// shared global bucket. Result is cached for 5 minutes per user; on cache
// miss or refresh, the access token is used to refetch. Returns null on
// auth failure so callers can decide between empty list and error response.
public sealed class UserGuildsService(
    IHttpClientFactory httpClientFactory,
    IMemoryCache cache) : IUserGuildsService
{
    private const ulong AdministratorPermission = 0x8;
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan ErrorTtl = TimeSpan.FromSeconds(10);

    public Task<IReadOnlyList<DiscordUserGuild>?> GetUserGuildsAsync(ulong discordId, string accessToken, CancellationToken ct = default)
    {
        var key = $"user-guilds:{discordId}";
        return cache.GetOrCoalesceAsync<IReadOnlyList<DiscordUserGuild>?>(key, CacheTtl, ErrorTtl, () => FetchAsync(accessToken, ct));
    }

    public Task<IReadOnlyList<DiscordUserGuild>?> GetForPrincipalAsync(ClaimsPrincipal user, CancellationToken ct = default)
    {
        if (!TryGetClaims(user, out var discordId, out var accessToken))
        {
            return Task.FromResult<IReadOnlyList<DiscordUserGuild>?>(null);
        }
        return GetUserGuildsAsync(discordId, accessToken, ct);
    }

    public async Task<bool> IsMemberAsync(ClaimsPrincipal user, ulong guildId, CancellationToken ct = default)
    {
        var list = await GetForPrincipalAsync(user, ct);
        return list is not null && list.Any(g => g.Id == guildId);
    }

    public async Task<bool> HasAdministratorAsync(ClaimsPrincipal user, ulong guildId, CancellationToken ct = default)
    {
        var list = await GetForPrincipalAsync(user, ct);
        var match = list?.FirstOrDefault(g => g.Id == guildId);
        return match is not null && HasAdministrator(match);
    }

    private static bool TryGetClaims(ClaimsPrincipal user, out ulong discordId, out string accessToken)
    {
        accessToken = "";
        discordId = 0;
        var rawId = user.FindFirst("discord_id")?.Value;
        if (!ulong.TryParse(rawId, out discordId))
        {
            return false;
        }
        accessToken = user.FindFirst("discord_access_token")?.Value ?? "";
        return !string.IsNullOrEmpty(accessToken);
    }

    private async Task<IReadOnlyList<DiscordUserGuild>?> FetchAsync(string accessToken, CancellationToken ct)
    {
        var client = httpClientFactory.CreateClient();
        client.Timeout = TimeSpan.FromSeconds(10);
        var request = new HttpRequestMessage(HttpMethod.Get, "https://discord.com/api/v10/users/@me/guilds");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        HttpResponseMessage response;
        try
        {
            response = await client.SendAsync(request, ct);
        }
        catch
        {
            return null;
        }

        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);

        var list = new List<DiscordUserGuild>();
        foreach (var el in doc.RootElement.EnumerateArray())
        {
            if (!el.TryGetProperty("id", out var idEl) || !ulong.TryParse(idEl.GetString(), out var id))
            {
                continue;
            }
            var name = el.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "";
            var icon = el.TryGetProperty("icon", out var i) && i.ValueKind != JsonValueKind.Null ? i.GetString() : null;
            var owner = el.TryGetProperty("owner", out var o) && o.GetBoolean();
            ulong permissions = 0;
            if (el.TryGetProperty("permissions", out var p))
            {
                if (p.ValueKind == JsonValueKind.String)
                {
                    ulong.TryParse(p.GetString(), out permissions);
                }
                else if (p.ValueKind == JsonValueKind.Number)
                {
                    p.TryGetUInt64(out permissions);
                }
            }
            list.Add(new DiscordUserGuild(id, name, icon, owner, permissions));
        }

        return list;
    }

    public static bool HasAdministrator(DiscordUserGuild guild)
    {
        return guild.Owner || (guild.Permissions & AdministratorPermission) == AdministratorPermission;
    }

    public static string? BuildIconUrl(ulong guildId, string? iconHash)
    {
        if (string.IsNullOrEmpty(iconHash))
        {
            return null;
        }
        var ext = iconHash.StartsWith("a_") ? "gif" : "png";
        return $"https://cdn.discordapp.com/icons/{guildId}/{iconHash}.{ext}";
    }
}
