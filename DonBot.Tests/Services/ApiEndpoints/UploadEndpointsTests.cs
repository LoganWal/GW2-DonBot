using System.Net;
using System.Security.Claims;
using System.Text;
using DonBot.Api.Endpoints;
using DonBot.Api.Services;
using DonBot.Core.Models.Entities;
using DonBot.Tests.Infrastructure;
using tusdotnet.Models;

namespace DonBot.Tests.Services.ApiEndpoints;

public class UploadEndpointsTests
{
    private sealed class FakeUserGuilds : IUserGuildsService
    {
        public HashSet<ulong> GuildIds { get; } = [];

        public Task<IReadOnlyList<DiscordUserGuild>?> GetUserGuildsAsync(ulong discordId, string accessToken, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<DiscordUserGuild>?>(BuildGuilds());

        public Task<IReadOnlyList<DiscordUserGuild>?> GetForPrincipalAsync(ClaimsPrincipal user, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<DiscordUserGuild>?>(BuildGuilds());

        public Task<bool> IsMemberAsync(ClaimsPrincipal user, ulong guildId, CancellationToken ct = default)
            => Task.FromResult(GuildIds.Contains(guildId));

        public Task<bool> HasAdministratorAsync(ClaimsPrincipal user, ulong guildId, CancellationToken ct = default)
            => Task.FromResult(false);

        private IReadOnlyList<DiscordUserGuild> BuildGuilds() => GuildIds
            .Select(id => new DiscordUserGuild(id, $"Guild {id}", null, false, 0))
            .ToList();
    }

    [Fact]
    public async Task ResolveTusGuildIdAsync_NoGuildMetadata_ReturnsGlobalGuild()
    {
        var result = await UploadEndpoints.ResolveTusGuildIdAsync(
            new Dictionary<string, Metadata>(),
            new ClaimsPrincipal(),
            new FakeUserGuilds(),
            CancellationToken.None);

        Assert.Equal(0, result.GuildId);
        Assert.Null(result.FailureStatus);
    }

    [Fact]
    public async Task ResolveTusGuildIdAsync_InvalidGuildMetadata_ReturnsBadRequest()
    {
        var result = await UploadEndpoints.ResolveTusGuildIdAsync(
            Metadata(("guildid", "not-a-number")),
            new ClaimsPrincipal(),
            new FakeUserGuilds(),
            CancellationToken.None);

        Assert.Equal(HttpStatusCode.BadRequest, result.FailureStatus);
    }

    [Fact]
    public async Task ResolveTusGuildIdAsync_GuildUserIsNotMember_ReturnsForbidden()
    {
        var result = await UploadEndpoints.ResolveTusGuildIdAsync(
            Metadata(("guildid", "42")),
            new ClaimsPrincipal(),
            new FakeUserGuilds(),
            CancellationToken.None);

        Assert.Equal(HttpStatusCode.Forbidden, result.FailureStatus);
    }

    [Fact]
    public async Task ResolveTusGuildIdAsync_GuildUserIsMember_ReturnsGuildId()
    {
        var userGuilds = new FakeUserGuilds();
        userGuilds.GuildIds.Add(42);

        var result = await UploadEndpoints.ResolveTusGuildIdAsync(
            Metadata(("guildid", "42")),
            new ClaimsPrincipal(),
            userGuilds,
            CancellationToken.None);

        Assert.Equal(42, result.GuildId);
        Assert.Null(result.FailureStatus);
    }

    [Fact]
    public async Task ResolveTusGuildIdAsync_CamelCaseGuildIdMetadata_ReturnsGuildId()
    {
        var userGuilds = new FakeUserGuilds();
        userGuilds.GuildIds.Add(42);

        var result = await UploadEndpoints.ResolveTusGuildIdAsync(
            Metadata(("guildId", "42")),
            new ClaimsPrincipal(),
            userGuilds,
            CancellationToken.None);

        Assert.Equal(42, result.GuildId);
        Assert.Null(result.FailureStatus);
    }

    [Fact]
    public async Task IsTusUploadOwnerAsync_MatchingDiscordId_ReturnsTrue()
    {
        using var db = new SqliteTestDb();
        await using (var ctx = await db.Factory.CreateDbContextAsync())
        {
            ctx.LogUpload.Add(new LogUpload
            {
                DiscordId = 123,
                TusFileId = "tus-1",
                FileName = "upload.zevtc",
                SourceType = "file",
                Status = "receiving"
            });
            await ctx.SaveChangesAsync();
        }

        var result = await UploadEndpoints.IsTusUploadOwnerAsync(
            db.Factory,
            "tus-1",
            123,
            CancellationToken.None);

        Assert.True(result);
    }

    [Fact]
    public async Task IsTusUploadOwnerAsync_DifferentDiscordId_ReturnsFalse()
    {
        using var db = new SqliteTestDb();
        await using (var ctx = await db.Factory.CreateDbContextAsync())
        {
            ctx.LogUpload.Add(new LogUpload
            {
                DiscordId = 123,
                TusFileId = "tus-1",
                FileName = "upload.zevtc",
                SourceType = "file",
                Status = "receiving"
            });
            await ctx.SaveChangesAsync();
        }

        var result = await UploadEndpoints.IsTusUploadOwnerAsync(
            db.Factory,
            "tus-1",
            456,
            CancellationToken.None);

        Assert.False(result);
    }

    private static Dictionary<string, Metadata> Metadata(params (string Key, string Value)[] values)
    {
        var header = string.Join(
            ",",
            values.Select(value =>
                $"{value.Key} {Convert.ToBase64String(Encoding.UTF8.GetBytes(value.Value))}"));

        return tusdotnet.Models.Metadata.Parse(header);
    }
}
