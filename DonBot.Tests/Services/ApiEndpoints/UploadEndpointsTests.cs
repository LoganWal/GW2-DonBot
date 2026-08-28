using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using DonBot.Api.Endpoints;
using DonBot.Api.Services;
using DonBot.Core.Models;
using DonBot.Core.Models.Entities;
using DonBot.Tests.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
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
    public async Task TusCreate_GuildDefaultsAcknowledgesAndPersistsDeliveryIntent()
    {
        var delivery = new FakeDiscordUploadDeliveryService
        {
            Validation = DiscordDeliveryValidationResult.Success()
        };
        using var host = NewLinkedGw2Host(delivery);
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/upload/tus");
        request.Headers.Add("Tus-Resumable", "1.0.0");
        request.Headers.Add("Upload-Length", "4");
        request.Headers.Add("Upload-Metadata", MetadataHeader(
            ("filename", "fight.zevtc"),
            ("guildid", "42"),
            ("discorddelivery", DiscordDeliveryModes.GuildDefaults)));
        request.Headers.Add("X-GW2-API-Key", "valid-key");

        var response = await host.Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal("accepted", response.Headers.GetValues("X-DonBot-Discord-Delivery").Single());
        await using var context = await host.DbFactory.CreateDbContextAsync();
        var upload = Assert.Single(context.LogUpload);
        Assert.Equal("receiving", upload.Status);
        Assert.Equal(DiscordDeliveryModes.GuildDefaults, upload.DiscordDeliveryMode);
        Assert.Null(upload.DiscordDeliveryChannelId);
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
    public async Task ListGw2UploadGuilds_ReturnsDiscordDeliveryCapabilityAndAuthorizedChannels()
    {
        var delivery = new FakeDiscordUploadDeliveryService
        {
            Capabilities = new DiscordDeliveryCapabilities(
                true,
                true,
                true,
                [DiscordDeliveryMessageKinds.PveSummary, DiscordDeliveryMessageKinds.WvwSummary],
                [new DiscordAuthorizedChannel(99, "logs")])
        };
        using var host = NewLinkedGw2Host(delivery);

        var response = await host.Client.PostAsJsonAsync("/api/upload/gw2/guilds", new { ApiKey = "valid-key" });
        var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains(
            "discord-summary-delivery-v1",
            json.RootElement.GetProperty("capabilities").EnumerateArray().Select(item => item.GetString()));
        var guild = json.RootElement.GetProperty("guilds").EnumerateArray()
            .Single(item => item.GetProperty("guildId").GetString() == "42");
        var discordDelivery = guild.GetProperty("discordDelivery");
        Assert.True(discordDelivery.GetProperty("enabled").GetBoolean());
        Assert.True(discordDelivery.GetProperty("defaultsAvailable").GetBoolean());
        Assert.True(discordDelivery.GetProperty("channelOverrideAllowed").GetBoolean());
        Assert.Equal("99", discordDelivery.GetProperty("channels")[0].GetProperty("channelId").GetString());
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

    [Fact]
    public async Task SubmitGw2Url_MissingKeyRejectsBeforeMalformedBodyIsRead()
    {
        using var host = NewHost();
        using var content = new StringContent("{malformed", Encoding.UTF8, "application/json");

        var response = await host.Client.PostAsync("/api/upload/gw2/url", content);
        var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("gw2_api_key_required", json.RootElement.GetProperty("error").GetString());
        Assert.DoesNotContain("malformed", json.RootElement.ToString());
    }

    [Fact]
    public async Task SubmitGw2Url_InvalidKeyReturnsGenericBadRequest()
    {
        var handler = new ApiStubHandler(_ => new HttpResponseMessage(HttpStatusCode.Unauthorized));
        using var host = NewHost(handler);

        var response = await PostGw2UrlAsync(host, apiKey: "secret-invalid-key");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("invalid_gw2_api_key", body);
        Assert.DoesNotContain("secret-invalid-key", body);
    }

    [Fact]
    public async Task SubmitGw2Url_UnlinkedAccountReturnsGenericForbidden()
    {
        using var host = NewHost(ValidGw2AccountHandler(Guid.NewGuid()));

        var response = await PostGw2UrlAsync(host, apiKey: "secret-valid-key");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Contains("gw2_account_not_linked", body);
        Assert.DoesNotContain("secret-valid-key", body);
        Assert.DoesNotContain("Player.1234", body);
    }

    [Fact]
    public async Task SubmitGw2Url_AllowedGuildCreatesExactUploadAndEnqueuesOnce()
    {
        using var host = NewLinkedGw2Host();

        var response = await PostGw2UrlAsync(host);
        var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        Assert.False(json.RootElement.GetProperty("duplicate").GetBoolean());
        Assert.Equal("pending", json.RootElement.GetProperty("status").GetString());
        Assert.Equal(JsonValueKind.Null, json.RootElement.GetProperty("fightLogId").ValueKind);
        var uploadId = json.RootElement.GetProperty("uploadId").GetInt64();
        Assert.True(uploadId > 0);

        await using var context = await host.DbFactory.CreateDbContextAsync();
        var upload = Assert.Single(await context.LogUpload.ToListAsync());
        Assert.Equal(123, upload.DiscordId);
        Assert.Equal(42, upload.GuildId);
        Assert.Equal("abc-123", upload.FileName);
        Assert.Equal("url", upload.SourceType);
        Assert.Equal("pending", upload.Status);
        Assert.Equal("https://dps.report/abc-123", upload.DpsReportUrl);
        Assert.False(upload.SubmitToWingman);

        var pipeline = host.Services.GetRequiredService<LogUploadPipelineService>();
        Assert.True(pipeline.TryReadQueuedUpload(out var queuedUploadId));
        Assert.Equal(uploadId, queuedUploadId);
        Assert.False(pipeline.TryReadQueuedUpload(out _));
    }

    [Fact]
    public async Task SubmitGw2Url_GuildDefaultsPersistsAcceptedDeliveryIntent()
    {
        var delivery = new FakeDiscordUploadDeliveryService
        {
            Validation = DiscordDeliveryValidationResult.Success()
        };
        using var host = NewLinkedGw2Host(delivery);

        var response = await PostGw2UrlAsync(
            host,
            discordDelivery: new { mode = DiscordDeliveryModes.GuildDefaults });
        var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        Assert.True(json.RootElement.GetProperty("discordDeliveryAccepted").GetBoolean());
        await using var context = await host.DbFactory.CreateDbContextAsync();
        var upload = Assert.Single(context.LogUpload);
        Assert.Equal(DiscordDeliveryModes.GuildDefaults, upload.DiscordDeliveryMode);
        Assert.Null(upload.DiscordDeliveryChannelId);
    }

    [Fact]
    public async Task SubmitGw2Url_ChannelOverridePersistsCanonicalChannel()
    {
        var delivery = new FakeDiscordUploadDeliveryService
        {
            Validation = DiscordDeliveryValidationResult.Success()
        };
        using var host = NewLinkedGw2Host(delivery);

        var response = await PostGw2UrlAsync(
            host,
            discordDelivery: new { mode = DiscordDeliveryModes.ChannelOverride, channelId = "99" });

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        await using var context = await host.DbFactory.CreateDbContextAsync();
        var upload = Assert.Single(context.LogUpload);
        Assert.Equal(DiscordDeliveryModes.ChannelOverride, upload.DiscordDeliveryMode);
        Assert.Equal(99, upload.DiscordDeliveryChannelId);
    }

    [Fact]
    public async Task SubmitGw2Url_UnknownDiscordDeliveryFieldReturnsBadRequest()
    {
        var delivery = new FakeDiscordUploadDeliveryService
        {
            Validation = DiscordDeliveryValidationResult.Success()
        };
        using var host = NewLinkedGw2Host(delivery);

        var response = await PostGw2UrlAsync(
            host,
            discordDelivery: new { mode = DiscordDeliveryModes.GuildDefaults, unexpected = true });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await using var context = await host.DbFactory.CreateDbContextAsync();
        Assert.Empty(context.LogUpload);
    }

    [Fact]
    public async Task SubmitGw2Url_DifferentDeliveryIntentReturnsConflictWithoutRetargeting()
    {
        var delivery = new FakeDiscordUploadDeliveryService
        {
            Validation = DiscordDeliveryValidationResult.Success()
        };
        using var host = NewLinkedGw2Host(delivery);
        var first = await PostGw2UrlAsync(
            host,
            discordDelivery: new { mode = DiscordDeliveryModes.GuildDefaults });
        Assert.Equal(HttpStatusCode.Accepted, first.StatusCode);

        var second = await PostGw2UrlAsync(
            host,
            discordDelivery: new { mode = DiscordDeliveryModes.ChannelOverride, channelId = "99" });
        var body = await second.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        Assert.Contains("discord_delivery_conflict", body);
        await using var context = await host.DbFactory.CreateDbContextAsync();
        var upload = Assert.Single(context.LogUpload);
        Assert.Equal(DiscordDeliveryModes.GuildDefaults, upload.DiscordDeliveryMode);
        Assert.Null(upload.DiscordDeliveryChannelId);
    }

    [Fact]
    public async Task SubmitGw2Url_ForbiddenGuildDoesNotCreateUpload()
    {
        using var host = NewLinkedGw2Host();

        var response = await PostGw2UrlAsync(host, guildId: "43");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        await using var context = await host.DbFactory.CreateDbContextAsync();
        Assert.Empty(context.LogUpload);
    }

    [Theory]
    [InlineData("http://dps.report/abc")]
    [InlineData("https://b.dps.report/abc")]
    [InlineData("https://wvw.report/abc")]
    [InlineData("https://gw2wingman.nevermindcreations.de/log/abc")]
    [InlineData("https://dps.report.evil.example/abc")]
    [InlineData("https://user@dps.report/abc")]
    [InlineData("https://dps.report/abc#fragment")]
    [InlineData("https://dps.report/abc?query=1")]
    [InlineData("https://dps.report/getJson?permalink=abc")]
    [InlineData("https://dps.report/abc\\evil")]
    [InlineData("https://dps.report/")]
    [InlineData("https://dps.report/abc\n")]
    public async Task SubmitGw2Url_NonCanonicalOrHostileUrlReturnsBadRequest(string url)
    {
        using var host = NewLinkedGw2Host();

        var response = await PostGw2UrlAsync(host, url: url);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData("")]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("+42")]
    [InlineData("042")]
    [InlineData(" 42")]
    [InlineData("9223372036854775808")]
    public async Task SubmitGw2Url_NonCanonicalGuildIdReturnsBadRequest(string guildId)
    {
        using var host = NewLinkedGw2Host();

        var response = await PostGw2UrlAsync(host, guildId: guildId);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task SubmitGw2Url_WingmanPropertyReturnsBadRequest()
    {
        using var host = NewLinkedGw2Host();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/upload/gw2/url");
        request.Headers.Add("X-GW2-API-Key", "valid-key");
        request.Content = JsonContent.Create(new
        {
            url = "https://dps.report/abc-123",
            guildId = "42",
            wingman = false
        });

        var response = await host.Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"url\":\"https://dps.report/abc-123\"}")]
    [InlineData("{\"guildId\":\"42\"}")]
    public async Task SubmitGw2Url_MissingRequestFieldReturnsBadRequest(string body)
    {
        using var host = NewLinkedGw2Host();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/upload/gw2/url");
        request.Headers.Add("X-GW2-API-Key", "valid-key");
        request.Content = new StringContent(body, Encoding.UTF8, "application/json");

        var response = await host.Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task SubmitGw2Url_ActiveDuplicateReturnsExistingReceiptWithoutSecondEnqueue()
    {
        using var host = NewLinkedGw2Host();
        var firstResponse = await PostGw2UrlAsync(host);
        var firstJson = JsonDocument.Parse(await firstResponse.Content.ReadAsStringAsync());
        var uploadId = firstJson.RootElement.GetProperty("uploadId").GetInt64();
        var pipeline = host.Services.GetRequiredService<LogUploadPipelineService>();
        Assert.True(pipeline.TryReadQueuedUpload(out _));

        var duplicateResponse = await PostGw2UrlAsync(host);
        var duplicateJson = JsonDocument.Parse(await duplicateResponse.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, duplicateResponse.StatusCode);
        Assert.Equal(uploadId, duplicateJson.RootElement.GetProperty("uploadId").GetInt64());
        Assert.True(duplicateJson.RootElement.GetProperty("duplicate").GetBoolean());
        Assert.Equal("pending", duplicateJson.RootElement.GetProperty("status").GetString());
        Assert.False(pipeline.TryReadQueuedUpload(out _));
        await using var context = await host.DbFactory.CreateDbContextAsync();
        Assert.Single(context.LogUpload);
    }

    [Fact]
    public async Task SubmitGw2Url_CompletedDuplicateReturnsRetainedFightReceipt()
    {
        using var host = NewLinkedGw2Host();
        await SeedUrlUploadAsync(host, "complete", fightLogId: 456);

        var response = await PostGw2UrlAsync(host);
        var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(json.RootElement.GetProperty("duplicate").GetBoolean());
        Assert.Equal("complete", json.RootElement.GetProperty("status").GetString());
        Assert.Equal(456, json.RootElement.GetProperty("fightLogId").GetInt64());
        Assert.False(host.Services.GetRequiredService<LogUploadPipelineService>().TryReadQueuedUpload(out _));
    }

    [Fact]
    public async Task SubmitGw2Url_CompletedDeliveryDuplicateReturnsNormalizedDeliveryReceipt()
    {
        var delivery = new FakeDiscordUploadDeliveryService
        {
            Validation = DiscordDeliveryValidationResult.Success(),
            Result = new DiscordDeliveryResult(true, "sent", 1, 0, 0, 0)
        };
        using var host = NewLinkedGw2Host(delivery);
        await using (var context = await host.DbFactory.CreateDbContextAsync())
        {
            var upload = BuildUrlUpload();
            upload.Status = "complete";
            upload.FightLogId = 456;
            upload.DiscordDeliveryMode = DiscordDeliveryModes.GuildDefaults;
            context.LogUpload.Add(upload);
            await context.SaveChangesAsync();
        }

        var response = await PostGw2UrlAsync(
            host,
            discordDelivery: new { mode = DiscordDeliveryModes.GuildDefaults });
        var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("sent", json.RootElement.GetProperty("discordDelivery").GetProperty("outcome").GetString());
        Assert.Equal(1, json.RootElement.GetProperty("discordDelivery").GetProperty("sent").GetInt32());
    }

    [Fact]
    public async Task SubmitGw2Url_FailedDuplicateReturnsStableConflict()
    {
        using var host = NewLinkedGw2Host();
        await SeedUrlUploadAsync(host, "failed");

        var response = await PostGw2UrlAsync(host);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Contains("import_failed", body);
        Assert.DoesNotContain("secret", body);
        Assert.False(host.Services.GetRequiredService<LogUploadPipelineService>().TryReadQueuedUpload(out _));
    }

    [Fact]
    public async Task SubmitGw2Url_ConcurrentIdenticalRequestsCreateOneImport()
    {
        using var host = NewLinkedGw2Host();

        var responses = await Task.WhenAll(
            PostGw2UrlAsync(host),
            PostGw2UrlAsync(host));

        Assert.Contains(responses, response => response.StatusCode == HttpStatusCode.Accepted);
        Assert.All(responses, response =>
            Assert.Contains(response.StatusCode, new[] { HttpStatusCode.Accepted, HttpStatusCode.OK }));
        await using var context = await host.DbFactory.CreateDbContextAsync();
        Assert.Single(context.LogUpload);
        var pipeline = host.Services.GetRequiredService<LogUploadPipelineService>();
        Assert.True(pipeline.TryReadQueuedUpload(out _));
        Assert.False(pipeline.TryReadQueuedUpload(out _));
    }

    [Fact]
    public async Task LogUpload_Gw2UrlImportIdentityIsUniqueButFileUploadIsExcluded()
    {
        using var db = new SqliteTestDb();
        await using (var context = await db.Factory.CreateDbContextAsync())
        {
            context.LogUpload.Add(BuildUrlUpload());
            await context.SaveChangesAsync();
        }

        await using (var duplicateContext = await db.Factory.CreateDbContextAsync())
        {
            duplicateContext.LogUpload.Add(BuildUrlUpload());
            await Assert.ThrowsAsync<DbUpdateException>(() => duplicateContext.SaveChangesAsync());
        }

        await using (var fileContext = await db.Factory.CreateDbContextAsync())
        {
            var fileUpload = BuildUrlUpload();
            fileUpload.SourceType = "file";
            fileContext.LogUpload.Add(fileUpload);
            await fileContext.SaveChangesAsync();
        }
    }

    [Fact]
    public async Task StreamProgress_CompletedUploadRequiresOwnerAuthentication()
    {
        using var host = NewHost();
        await SeedUrlUploadAsync(host, "complete", fightLogId: 77);

        var anonymous = await host.Client.GetAsync("/api/upload/stream/1");

        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);

        host.AuthenticateAs(456);
        var differentUser = await host.Client.GetAsync("/api/upload/stream/1");

        Assert.Equal(HttpStatusCode.NotFound, differentUser.StatusCode);
    }

    [Fact]
    public async Task StreamProgress_CompletedUploadReturnsDurableResultToOwner()
    {
        var delivery = new FakeDiscordUploadDeliveryService
        {
            Result = new DiscordDeliveryResult(true, "sent", 1, 0, 0, 0)
        };
        using var host = NewHost(discordDelivery: delivery);
        await SeedUrlUploadAsync(host, "complete", fightLogId: 77);
        host.AuthenticateAs(123);

        var response = await host.Client.GetAsync("/api/upload/stream/1");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("https://dps.report/abc-123", body);
        Assert.Contains("\"fightLogId\":77", body);
        Assert.Contains("\"outcome\":\"sent\"", body);
    }

    [Fact]
    public async Task StreamProgress_Gw2ApiKeyAuthenticatesMannyUploaderOwner()
    {
        using var host = NewLinkedGw2Host();
        await SeedUrlUploadAsync(host, "complete", fightLogId: 77);
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/upload/stream/1");
        request.Headers.Add("X-GW2-API-Key", "valid-key");

        var response = await host.Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task WriteProgressStreamAsync_RequestCancellationCompletesNormally()
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await UploadEndpoints.WriteProgressStreamAsync(
            1,
            new LogUploadProgressService(),
            context.Response,
            cts.Token);
    }

    private static MinimalApiHost NewLinkedGw2Host(FakeDiscordUploadDeliveryService? discordDelivery = null)
    {
        var accountId = Guid.NewGuid();
        var memberships = new FakeDiscordGuildMembershipService();
        memberships.GuildIds.Add(42);
        var host = NewHost(ValidGw2AccountHandler(accountId), memberships, discordDelivery);
        using var context = host.DbFactory.CreateDbContext();
        context.GuildWarsAccount.Add(new GuildWarsAccount
        {
            GuildWarsAccountId = accountId,
            DiscordId = 123,
            GuildWarsAccountName = "Player.1234"
        });
        context.Guild.AddRange(
            new Guild { GuildId = 42, GuildName = "Allowed Guild" },
            new Guild { GuildId = 43, GuildName = "Forbidden Guild" });
        context.SaveChanges();
        return host;
    }

    private static HttpMessageHandler ValidGw2AccountHandler(Guid accountId) =>
        new ApiStubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                $$"""{"id":"{{accountId}}","name":"Player.1234","world":2202}""",
                Encoding.UTF8,
                "application/json")
        });

    private static async Task<HttpResponseMessage> PostGw2UrlAsync(
        MinimalApiHost host,
        string url = "https://dps.report/abc-123",
        string guildId = "42",
        string apiKey = "valid-key",
        object? discordDelivery = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/upload/gw2/url");
        request.Headers.Add("X-GW2-API-Key", apiKey);
        request.Content = JsonContent.Create(new { url, guildId, discordDelivery });
        return await host.Client.SendAsync(request);
    }

    private static async Task SeedUrlUploadAsync(MinimalApiHost host, string status, long? fightLogId = null)
    {
        await using var context = await host.DbFactory.CreateDbContextAsync();
        var upload = BuildUrlUpload();
        upload.Status = status;
        upload.FightLogId = fightLogId;
        context.LogUpload.Add(upload);
        await context.SaveChangesAsync();
    }

    private static LogUpload BuildUrlUpload() => new()
    {
        DiscordId = 123,
        GuildId = 42,
        FileName = "abc-123",
        SourceType = "url",
        Status = "pending",
        DpsReportUrl = "https://dps.report/abc-123",
        SubmitToWingman = false
    };

    private static MinimalApiHost NewHost(
        HttpMessageHandler? gw2Handler = null,
        FakeDiscordGuildMembershipService? discordGuilds = null,
        FakeDiscordUploadDeliveryService? discordDelivery = null) =>
        new(
            app => app.MapUploadEndpoints(),
            services =>
            {
                services.AddSingleton<ILogUploadProgressService, LogUploadProgressService>();
                services.AddSingleton<LogUploadPipelineService>();
                services.AddSingleton<IDiscordGuildMembershipService>(discordGuilds ?? new FakeDiscordGuildMembershipService());
                services.AddSingleton<IDiscordUploadDeliveryService>(discordDelivery ?? new FakeDiscordUploadDeliveryService());
            },
            httpHandler: gw2Handler);

    private static Dictionary<string, Metadata> Metadata(params (string Key, string Value)[] values)
    {
        return tusdotnet.Models.Metadata.Parse(MetadataHeader(values));
    }

    private static string MetadataHeader(params (string Key, string Value)[] values)
    {
        return string.Join(
            ",",
            values.Select(value =>
                $"{value.Key} {Convert.ToBase64String(Encoding.UTF8.GetBytes(value.Value))}"));
    }

    private sealed class ApiStubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(respond(request));
    }
}
