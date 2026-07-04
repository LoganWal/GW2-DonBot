using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using DonBot.Api.Endpoints;
using DonBot.Api.Services;
using DonBot.Core.Models.Entities;
using DonBot.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
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

    private sealed class FakeDiscordGuildMembershipService : IDiscordGuildMembershipService
    {
        public HashSet<long> GuildIds { get; } = [];

        public Task<IReadOnlySet<long>> GetMemberGuildIdsAsync(
            long discordId,
            IReadOnlyCollection<long> guildIds,
            CancellationToken ct = default)
        {
            var result = guildIds
                .Where(GuildIds.Contains)
                .ToHashSet();
            return Task.FromResult<IReadOnlySet<long>>(result);
        }
    }

    [Fact]
    public async Task SubmitUrls_ReportUrlWithTrailingPunctuation_StoresCanonicalUrl()
    {
        using var host = NewHost();
        host.AuthenticateAs(123L);

        var response = await host.Client.PostAsJsonAsync("/api/upload/urls", new
        {
            Urls = new[] { "https://b.dps.report/abc," },
            Wingman = false
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await using var ctx = await host.DbFactory.CreateDbContextAsync();
        var upload = Assert.Single(ctx.LogUpload);
        Assert.Equal("https://dps.report/abc", upload.DpsReportUrl);
        Assert.Equal("abc", upload.FileName);
        Assert.False(upload.SubmitToWingman);
    }

    [Fact]
    public async Task SubmitUrls_CanonicalEquivalentReportUrls_CreatesSingleUpload()
    {
        using var host = NewHost();
        host.AuthenticateAs(123L);

        var response = await host.Client.PostAsJsonAsync("/api/upload/urls", new
        {
            Urls = new[]
            {
                "https://b.dps.report/abc",
                "https://dps.report/abc",
                "https://dps.report/getJson?permalink=abc"
            },
            Wingman = true
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var result = Assert.Single(body.RootElement.EnumerateArray());
        Assert.Equal("abc", result.GetProperty("fileName").GetString());

        await using var ctx = await host.DbFactory.CreateDbContextAsync();
        var upload = Assert.Single(ctx.LogUpload);
        Assert.Equal("https://dps.report/abc", upload.DpsReportUrl);
        Assert.Equal("abc", upload.FileName);
        Assert.True(upload.SubmitToWingman);
    }

    [Fact]
    public async Task SubmitUrls_ReportRootUrl_ReturnsBadRequest()
    {
        using var host = NewHost();
        host.AuthenticateAs(123L);

        var response = await host.Client.PostAsJsonAsync("/api/upload/urls", new
        {
            Urls = new[] { "https://dps.report/" }
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
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
    public void ResolveTusGuildIdAsync_Gw2KeyAllowedGuild_ReturnsGuildId()
    {
        var result = UploadEndpoints.ResolveTusGuildIdAsync(
            Metadata(("guildid", "42")),
            new HashSet<long> { 42 });

        Assert.Equal(42, result.GuildId);
        Assert.Null(result.FailureStatus);
    }

    [Fact]
    public void ResolveTusGuildIdAsync_Gw2KeyNoGuildMetadata_ReturnsBadRequest()
    {
        var result = UploadEndpoints.ResolveTusGuildIdAsync(
            new Dictionary<string, Metadata>(),
            new HashSet<long> { 42 });

        Assert.Equal(HttpStatusCode.BadRequest, result.FailureStatus);
    }

    [Fact]
    public void ResolveTusGuildIdAsync_Gw2KeyDisallowedGuild_ReturnsForbidden()
    {
        var result = UploadEndpoints.ResolveTusGuildIdAsync(
            Metadata(("guildid", "42")),
            new HashSet<long> { 43 });

        Assert.Equal(HttpStatusCode.Forbidden, result.FailureStatus);
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

    [Fact]
    public async Task ListGw2UploadGuilds_ValidLinkedKey_ReturnsDiscordMemberGuilds()
    {
        var accountId = Guid.NewGuid();
        var handler = new ApiStubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                $$"""{"id":"{{accountId}}","name":"Player.1234","world":2202,"guilds":["live-guild"]}""",
                Encoding.UTF8,
                "application/json")
        });
        var discordGuilds = new FakeDiscordGuildMembershipService();
        discordGuilds.GuildIds.UnionWith([10, 11]);
        using var host = NewHost(handler, discordGuilds);
        await using (var db = await host.DbFactory.CreateDbContextAsync())
        {
            db.GuildWarsAccount.Add(new GuildWarsAccount
            {
                GuildWarsAccountId = accountId,
                DiscordId = 123,
                GuildWarsAccountName = "Player.1234",
                GuildWarsGuilds = "stored-guild"
            });
            db.Guild.AddRange(
                new Guild
                {
                    GuildId = 10,
                    GuildName = "Live Guild",
                    Gw2GuildMemberRoleId = "live-guild"
                },
                new Guild
                {
                    GuildId = 11,
                    GuildName = "Stored Guild",
                    Gw2SecondaryMemberRoleIds = "stored-guild"
                },
                new Guild
                {
                    GuildId = 12,
                    GuildName = "Other Guild",
                    Gw2GuildMemberRoleId = "other-guild"
                });
            await db.SaveChangesAsync();
        }

        var response = await host.Client.PostAsJsonAsync("/api/upload/gw2/guilds", new { ApiKey = "valid-key" });
        var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Player.1234", json.RootElement.GetProperty("accountName").GetString());
        var guilds = json.RootElement.GetProperty("guilds").EnumerateArray().ToList();
        Assert.Equal(2, guilds.Count);
        Assert.Contains(guilds, g => g.GetProperty("guildId").GetString() == "10");
        Assert.Contains(guilds, g => g.GetProperty("guildId").GetString() == "11");
        Assert.DoesNotContain(guilds, g => g.GetProperty("guildId").GetString() == "12");
    }

    [Fact]
    public async Task ListGw2UploadGuilds_InvalidKey_ReturnsBadRequest()
    {
        var handler = new ApiStubHandler(_ => new HttpResponseMessage(HttpStatusCode.Unauthorized));
        using var host = NewHost(handler);

        var response = await host.Client.PostAsJsonAsync("/api/upload/gw2/guilds", new { ApiKey = "bad-key" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ListGw2UploadGuilds_UnlinkedAccount_ReturnsForbidden()
    {
        var accountId = Guid.NewGuid();
        var handler = new ApiStubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                $$"""{"id":"{{accountId}}","name":"Player.1234","world":2202,"guilds":["live-guild"]}""",
                Encoding.UTF8,
                "application/json")
        });
        using var host = NewHost(handler);

        var response = await host.Client.PostAsJsonAsync("/api/upload/gw2/guilds", new { ApiKey = "valid-key" });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    private static MinimalApiHost NewHost(
        HttpMessageHandler? gw2Handler = null,
        FakeDiscordGuildMembershipService? discordGuilds = null) =>
        new(
            app => app.MapUploadEndpoints(),
            services =>
            {
                services.AddSingleton<ILogUploadProgressService, LogUploadProgressService>();
                services.AddSingleton<LogUploadPipelineService>();
                services.AddSingleton<IDiscordGuildMembershipService>(discordGuilds ?? new FakeDiscordGuildMembershipService());
            },
            httpHandler: gw2Handler);

    private static Dictionary<string, Metadata> Metadata(params (string Key, string Value)[] values)
    {
        var header = string.Join(
            ",",
            values.Select(value =>
                $"{value.Key} {Convert.ToBase64String(Encoding.UTF8.GetBytes(value.Value))}"));

        return tusdotnet.Models.Metadata.Parse(header);
    }

    private sealed class ApiStubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(respond(request));
    }
}
