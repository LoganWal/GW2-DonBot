using System.Security.Claims;
using DonBot.Api.Services;
using Microsoft.Extensions.Caching.Memory;

namespace DonBot.Tests.Services.ApiServices;

public sealed class AccessibleGuildsCacheTests
{
    [Fact]
    public async Task GetAsync_RequestCancellationDoesNotCancelCachedBuild()
    {
        var userGuilds = new FakeUserGuilds();
        var cache = new AccessibleGuildsCache(userGuilds, new MemoryCache(new MemoryCacheOptions()));
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        var result = await cache.GetAsync(
            User(),
            "test-guilds",
            TimeSpan.FromMinutes(1),
            TimeSpan.FromSeconds(1),
            (_, ct) => Task.FromResult<IReadOnlyList<string>>([ct.IsCancellationRequested ? "canceled" : "active"]),
            cts.Token);

        Assert.False(result.IsUnauthorized);
        Assert.Equal(["active"], result.Guilds);
        Assert.Equal(CancellationToken.None, userGuilds.LastToken);
    }

    private static ClaimsPrincipal User() =>
        new(new ClaimsIdentity([
            new Claim("discord_id", "123"),
            new Claim("discord_access_token", "token")
        ], "test"));

    private sealed class FakeUserGuilds : IUserGuildsService
    {
        public CancellationToken LastToken { get; private set; }

        public Task<IReadOnlyList<DiscordUserGuild>?> GetUserGuildsAsync(
            ulong discordId,
            string accessToken,
            CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<DiscordUserGuild>?>([]);

        public Task<IReadOnlyList<DiscordUserGuild>?> GetForPrincipalAsync(
            ClaimsPrincipal user,
            CancellationToken ct = default)
        {
            LastToken = ct;
            return Task.FromResult<IReadOnlyList<DiscordUserGuild>?>([
                new DiscordUserGuild(1, "One", null, false, 0)
            ]);
        }

        public Task<bool> IsMemberAsync(ClaimsPrincipal user, ulong guildId, CancellationToken ct = default) =>
            Task.FromResult(false);

        public Task<bool> HasAdministratorAsync(ClaimsPrincipal user, ulong guildId, CancellationToken ct = default) =>
            Task.FromResult(false);
    }
}
