using System.Net;
using System.Security.Claims;
using System.Text.Json;
using DonBot.Api.Endpoints;
using DonBot.Api.Services;
using DonBot.Core.Services.RaidLifecycle;
using DonBot.Models.Entities;
using DonBot.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DonBot.Tests.Services.ApiEndpoints;

public class LiveRaidEndpointsIntegrationTests
{
    private const long TestDiscordId = 5555L;

    private sealed class FakeUserGuilds : IUserGuildsService
    {
        public HashSet<long> Allowed { get; } = new();

        private IReadOnlyList<DiscordUserGuild> Build() => Allowed
            .Select(id => new DiscordUserGuild((ulong)id, $"Guild{id}", null, false, 0))
            .ToList();

        public Task<IReadOnlyList<DiscordUserGuild>?> GetUserGuildsAsync(ulong discordId, string accessToken, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<DiscordUserGuild>?>(Build());

        public Task<IReadOnlyList<DiscordUserGuild>?> GetForPrincipalAsync(ClaimsPrincipal user, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<DiscordUserGuild>?>(Build());

        public Task<bool> IsMemberAsync(ClaimsPrincipal user, ulong guildId, CancellationToken ct = default)
            => Task.FromResult(Allowed.Contains((long)guildId));

        public Task<bool> HasAdministratorAsync(ClaimsPrincipal user, ulong guildId, CancellationToken ct = default)
            => Task.FromResult(Allowed.Contains((long)guildId));
    }

    private sealed class NoopRaidNotifier : IRaidNotifier
    {
        public int StartedCalls { get; private set; }
        public int EndedCalls { get; private set; }

        public Task PostRaidStartedAsync(long guildId, CancellationToken ct = default)
        {
            StartedCalls++;
            return Task.CompletedTask;
        }

        public Task PostRaidEndedAsync(FightsReport closedReport, CancellationToken ct = default)
        {
            EndedCalls++;
            return Task.CompletedTask;
        }
    }

    private sealed class TestHost : IDisposable
    {
        public MinimalApiHost Inner { get; }
        public FakeUserGuilds Membership { get; } = new();
        public NoopRaidNotifier RaidNotifier { get; } = new();

        public TestHost()
        {
            var membership = Membership;
            var notifier = RaidNotifier;
            Inner = new MinimalApiHost(
                app => app.MapLiveRaidEndpoints(),
                services =>
                {
                    services.AddScoped<IRaidLifecycleService, RaidLifecycleService>();
                    services.AddSingleton<IUserGuildsService>(membership);
                    services.AddSingleton<IRaidNotifier>(notifier);
                });
            Inner.AuthenticateAs(TestDiscordId);
        }

        public IDbContextFactory<DatabaseContext> DbFactory => Inner.DbFactory;
        public System.Net.Http.HttpClient Client => Inner.Client;

        public void AllowMembership(params long[] guildIds)
        {
            foreach (var id in guildIds)
            {
                Membership.Allowed.Add(id);
            }
        }

        public void Dispose() => Inner.Dispose();
    }

    private static TestHost NewHost() => new();

    [Fact]
    public async Task ListGuilds_ReturnsOnlyMemberGuildsWithFightsReports()
    {
        using var host = NewHost();
        host.AllowMembership(1L); // user is in guild 1 only
        await using (var db = await host.DbFactory.CreateDbContextAsync())
        {
            db.Guild.Add(new Guild { GuildId = 1L, GuildName = "Has Raids" });
            db.Guild.Add(new Guild { GuildId = 2L, GuildName = "Other Guild" });
            db.FightsReport.Add(new FightsReport { GuildId = 1L, FightsStart = DateTime.UtcNow.AddHours(-1) });
            db.FightsReport.Add(new FightsReport { GuildId = 2L, FightsStart = DateTime.UtcNow.AddHours(-1) });
            await db.SaveChangesAsync();
        }

        var body = await host.Client.GetStringAsync("/api/live-raid/guilds");
        var arr = JsonDocument.Parse(body).RootElement;

        Assert.Equal(1, arr.GetArrayLength());
        Assert.Equal("1", arr[0].GetProperty("guildId").GetString());
        Assert.Equal("Has Raids", arr[0].GetProperty("guildName").GetString());
    }

    [Fact]
    public async Task ListGuilds_UserNotInAnyTrackedGuild_ReturnsEmpty()
    {
        using var host = NewHost();
        await using (var db = await host.DbFactory.CreateDbContextAsync())
        {
            db.Guild.Add(new Guild { GuildId = 1L, GuildName = "Has Raids" });
            db.FightsReport.Add(new FightsReport { GuildId = 1L, FightsStart = DateTime.UtcNow.AddHours(-1) });
            await db.SaveChangesAsync();
        }

        var body = await host.Client.GetStringAsync("/api/live-raid/guilds");
        var arr = JsonDocument.Parse(body).RootElement;
        Assert.Equal(0, arr.GetArrayLength());
    }

    [Fact]
    public async Task ListGuilds_NoAuth_Returns401()
    {
        using var host = NewHost();
        host.Inner.Client.DefaultRequestHeaders.Authorization = null;
        var response = await host.Client.GetAsync("/api/live-raid/guilds");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetLatestRaid_NoRaids_Returns404()
    {
        using var host = NewHost();
        host.AllowMembership(42L);
        var response = await host.Client.GetAsync("/api/live-raid/42");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetLatestRaid_MemberOfGuild_ReturnsMostRecentReport()
    {
        using var host = NewHost();
        host.AllowMembership(42L);
        var start = DateTime.UtcNow.AddMinutes(-30);
        await using (var db = await host.DbFactory.CreateDbContextAsync())
        {
            db.FightsReport.Add(new FightsReport { GuildId = 42L, FightsStart = DateTime.UtcNow.AddHours(-3), FightsEnd = DateTime.UtcNow.AddHours(-2) });
            db.FightsReport.Add(new FightsReport { GuildId = 42L, FightsStart = start });
            db.FightLog.Add(new FightLog { FightLogId = 1, GuildId = 42L, FightStart = start.AddMinutes(5), Url = "u1" });
            db.FightLog.Add(new FightLog { FightLogId = 2, GuildId = 42L, FightStart = start.AddMinutes(10), Url = "u2" });
            db.FightLog.Add(new FightLog { FightLogId = 3, GuildId = 42L, FightStart = start.AddHours(-3), Url = "u3" });
            db.FightLog.Add(new FightLog { FightLogId = 4, GuildId = 99L, FightStart = start.AddMinutes(5), Url = "u4" });
            await db.SaveChangesAsync();
        }

        var body = await host.Client.GetStringAsync("/api/live-raid/42");
        var doc = JsonDocument.Parse(body).RootElement;

        Assert.True(doc.GetProperty("isOpen").GetBoolean());
        var ids = doc.GetProperty("fightLogIds").EnumerateArray().Select(e => e.GetInt64()).ToList();
        Assert.Equal(new[] { 1L, 2L }, ids.ToArray());
    }

    [Fact]
    public async Task GetLatestRaid_NotMemberOfGuild_Returns403()
    {
        using var host = NewHost();
        await using (var db = await host.DbFactory.CreateDbContextAsync())
        {
            db.FightsReport.Add(new FightsReport { GuildId = 42L, FightsStart = DateTime.UtcNow.AddHours(-1) });
            await db.SaveChangesAsync();
        }

        var response = await host.Client.GetAsync("/api/live-raid/42");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetLatestRaid_GuildIdSerializedAsString()
    {
        // Discord snowflakes exceed Number.MAX_SAFE_INTEGER in JS, so the API must return
        // guildId as a string. Otherwise the browser silently truncates the last digits.
        using var host = NewHost();
        var bigGuildId = 415441457151737870L;
        host.AllowMembership(bigGuildId);
        await using (var db = await host.DbFactory.CreateDbContextAsync())
        {
            db.FightsReport.Add(new FightsReport { GuildId = bigGuildId, FightsStart = DateTime.UtcNow.AddMinutes(-10) });
            await db.SaveChangesAsync();
        }

        var body = await host.Client.GetStringAsync($"/api/live-raid/{bigGuildId}");
        var doc = JsonDocument.Parse(body).RootElement;

        Assert.Equal(JsonValueKind.String, doc.GetProperty("guildId").ValueKind);
        Assert.Equal(bigGuildId.ToString(), doc.GetProperty("guildId").GetString());
    }

    [Fact]
    public async Task GetLatestRaid_ClosedReport_OnlyIncludesFightsInWindow()
    {
        using var host = NewHost();
        host.AllowMembership(42L);
        var start = DateTime.UtcNow.AddHours(-2);
        var end = DateTime.UtcNow.AddHours(-1);
        await using (var db = await host.DbFactory.CreateDbContextAsync())
        {
            db.FightsReport.Add(new FightsReport { GuildId = 42L, FightsStart = start, FightsEnd = end });
            db.FightLog.Add(new FightLog { FightLogId = 1, GuildId = 42L, FightStart = start.AddMinutes(10), Url = "u1" });
            db.FightLog.Add(new FightLog { FightLogId = 2, GuildId = 42L, FightStart = end.AddMinutes(30), Url = "u2" });
            await db.SaveChangesAsync();
        }

        var body = await host.Client.GetStringAsync("/api/live-raid/42");
        var doc = JsonDocument.Parse(body).RootElement;

        Assert.False(doc.GetProperty("isOpen").GetBoolean());
        var ids = doc.GetProperty("fightLogIds").EnumerateArray().Select(e => e.GetInt64()).ToList();
        Assert.Equal(new[] { 1L }, ids.ToArray());
    }

    [Fact]
    public async Task GetSingleLog_ForeignGuild_Returns404()
    {
        using var host = NewHost();
        host.AllowMembership(42L);
        var start = DateTime.UtcNow.AddMinutes(-30);
        await using (var db = await host.DbFactory.CreateDbContextAsync())
        {
            db.FightsReport.Add(new FightsReport { GuildId = 42L, FightsStart = start });
            db.FightLog.Add(new FightLog { FightLogId = 1, GuildId = 99L, FightStart = start.AddMinutes(5), Url = "u1" });
            await db.SaveChangesAsync();
        }

        var response = await host.Client.GetAsync("/api/live-raid/42/logs/1");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetSingleLog_OwnedByGuild_ReturnsLog()
    {
        using var host = NewHost();
        host.AllowMembership(42L);
        var start = DateTime.UtcNow.AddMinutes(-30);
        await using (var db = await host.DbFactory.CreateDbContextAsync())
        {
            db.FightsReport.Add(new FightsReport { GuildId = 42L, FightsStart = start });
            db.FightLog.Add(new FightLog { FightLogId = 7, GuildId = 42L, FightStart = start.AddMinutes(5), Url = "u7" });
            await db.SaveChangesAsync();
        }

        var body = await host.Client.GetStringAsync("/api/live-raid/42/logs/7");
        var doc = JsonDocument.Parse(body).RootElement;

        Assert.Equal(7, doc.GetProperty("log").GetProperty("fightLogId").GetInt64());
    }

    [Fact]
    public async Task GetSingleLog_OutsideReportWindow_Returns404()
    {
        using var host = NewHost();
        host.AllowMembership(42L);
        var start = DateTime.UtcNow.AddMinutes(-30);
        await using (var db = await host.DbFactory.CreateDbContextAsync())
        {
            db.FightsReport.Add(new FightsReport { GuildId = 42L, FightsStart = start });
            db.FightLog.Add(new FightLog { FightLogId = 7, GuildId = 42L, FightStart = start.AddHours(-5), Url = "u7" });
            await db.SaveChangesAsync();
        }

        var response = await host.Client.GetAsync("/api/live-raid/42/logs/7");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetSingleLog_NoReport_Returns404()
    {
        using var host = NewHost();
        host.AllowMembership(42L);
        await using (var db = await host.DbFactory.CreateDbContextAsync())
        {
            db.FightLog.Add(new FightLog { FightLogId = 7, GuildId = 42L, FightStart = DateTime.UtcNow, Url = "u7" });
            await db.SaveChangesAsync();
        }

        var response = await host.Client.GetAsync("/api/live-raid/42/logs/7");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetAggregate_NoLogsInWindow_Returns404()
    {
        using var host = NewHost();
        host.AllowMembership(42L);
        await using (var db = await host.DbFactory.CreateDbContextAsync())
        {
            db.FightsReport.Add(new FightsReport { GuildId = 42L, FightsStart = DateTime.UtcNow.AddHours(-1) });
            await db.SaveChangesAsync();
        }

        var response = await host.Client.GetAsync("/api/live-raid/42/aggregate");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task StartRaid_NoAuth_Returns401()
    {
        using var host = NewHost();
        host.Inner.Client.DefaultRequestHeaders.Authorization = null;
        var response = await host.Client.PostAsync("/api/live-raid/42/start", null);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task StopRaid_NoAuth_Returns401()
    {
        using var host = NewHost();
        host.Inner.Client.DefaultRequestHeaders.Authorization = null;
        var response = await host.Client.PostAsync("/api/live-raid/42/stop", null);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task StartRaid_Success_FiresRaidAlertNotifier()
    {
        using var host = NewHost();
        host.AllowMembership(42L);
        await using (var db = await host.DbFactory.CreateDbContextAsync())
        {
            db.Guild.Add(new Guild { GuildId = 42L, GuildName = "Test" });
            await db.SaveChangesAsync();
        }

        var response = await host.Client.PostAsync("/api/live-raid/42/start", null);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(1, host.RaidNotifier.StartedCalls);
    }

    [Fact]
    public async Task StartRaid_AlreadyOpen_DoesNotFireRaidAlertNotifier()
    {
        using var host = NewHost();
        host.AllowMembership(42L);
        await using (var db = await host.DbFactory.CreateDbContextAsync())
        {
            db.Guild.Add(new Guild { GuildId = 42L, GuildName = "Test" });
            db.FightsReport.Add(new FightsReport { GuildId = 42L, FightsStart = DateTime.UtcNow.AddMinutes(-10) });
            await db.SaveChangesAsync();
        }

        var response = await host.Client.PostAsync("/api/live-raid/42/start", null);
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(0, host.RaidNotifier.StartedCalls);
    }

    [Fact]
    public async Task StopRaid_Success_FiresRaidEndedNotifier()
    {
        using var host = NewHost();
        host.AllowMembership(42L);
        await using (var db = await host.DbFactory.CreateDbContextAsync())
        {
            db.Guild.Add(new Guild { GuildId = 42L, GuildName = "Test" });
            db.FightsReport.Add(new FightsReport { GuildId = 42L, FightsStart = DateTime.UtcNow.AddMinutes(-30) });
            await db.SaveChangesAsync();
        }

        var response = await host.Client.PostAsync("/api/live-raid/42/stop", null);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(1, host.RaidNotifier.EndedCalls);
    }

    [Fact]
    public async Task StopRaid_NoOpenRaid_DoesNotFireRaidEndedNotifier()
    {
        using var host = NewHost();
        host.AllowMembership(42L);

        var response = await host.Client.PostAsync("/api/live-raid/42/stop", null);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(0, host.RaidNotifier.EndedCalls);
    }

    [Fact]
    public async Task StartRaid_NotMember_Returns403()
    {
        using var host = NewHost();
        var response = await host.Client.PostAsync("/api/live-raid/42/start", null);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task ListGuilds_GuildRowMissing_DropsThatGuild()
    {
        using var host = NewHost();
        host.AllowMembership(1L, 2L);
        await using (var db = await host.DbFactory.CreateDbContextAsync())
        {
            db.Guild.Add(new Guild { GuildId = 1L, GuildName = "Tracked" });
            db.FightsReport.Add(new FightsReport { GuildId = 1L, FightsStart = DateTime.UtcNow.AddHours(-1) });
            db.FightsReport.Add(new FightsReport { GuildId = 2L, FightsStart = DateTime.UtcNow.AddHours(-1) });
            await db.SaveChangesAsync();
        }

        var body = await host.Client.GetStringAsync("/api/live-raid/guilds");
        var arr = JsonDocument.Parse(body).RootElement;

        Assert.Equal(1, arr.GetArrayLength());
        Assert.Equal("1", arr[0].GetProperty("guildId").GetString());
    }

    [Fact]
    public async Task GetLatestRaid_OpenReportNoFights_ReturnsEmptyFightLogIds()
    {
        using var host = NewHost();
        host.AllowMembership(42L);
        await using (var db = await host.DbFactory.CreateDbContextAsync())
        {
            db.FightsReport.Add(new FightsReport { GuildId = 42L, FightsStart = DateTime.UtcNow.AddMinutes(-5) });
            await db.SaveChangesAsync();
        }

        var body = await host.Client.GetStringAsync("/api/live-raid/42");
        var doc = JsonDocument.Parse(body).RootElement;

        Assert.True(doc.GetProperty("isOpen").GetBoolean());
        Assert.Equal(0, doc.GetProperty("fightLogIds").GetArrayLength());
    }

    [Fact]
    public async Task GetLatestRaid_OpenReportWithOlderStart_StillPreferredOverNewerClosed()
    {
        using var host = NewHost();
        host.AllowMembership(42L);
        await using (var db = await host.DbFactory.CreateDbContextAsync())
        {
            db.FightsReport.Add(new FightsReport { GuildId = 42L, FightsStart = DateTime.UtcNow.AddHours(-3) });
            db.FightsReport.Add(new FightsReport
            {
                GuildId = 42L,
                FightsStart = DateTime.UtcNow.AddHours(-1),
                FightsEnd = DateTime.UtcNow.AddMinutes(-30)
            });
            await db.SaveChangesAsync();
        }

        var body = await host.Client.GetStringAsync("/api/live-raid/42");
        var doc = JsonDocument.Parse(body).RootElement;

        Assert.True(doc.GetProperty("isOpen").GetBoolean());
    }

    [Fact]
    public async Task GetLatestRaid_OnlyClosedReports_ReturnsTheMostRecentClosed()
    {
        using var host = NewHost();
        host.AllowMembership(42L);
        var start = DateTime.UtcNow.AddHours(-2);
        var end = DateTime.UtcNow.AddHours(-1);
        await using (var db = await host.DbFactory.CreateDbContextAsync())
        {
            db.FightsReport.Add(new FightsReport
            {
                GuildId = 42L,
                FightsStart = DateTime.UtcNow.AddHours(-8),
                FightsEnd = DateTime.UtcNow.AddHours(-7)
            });
            db.FightsReport.Add(new FightsReport { GuildId = 42L, FightsStart = start, FightsEnd = end });
            db.FightLog.Add(new FightLog { FightLogId = 1, GuildId = 42L, FightStart = start.AddMinutes(15), Url = "u1" });
            await db.SaveChangesAsync();
        }

        var body = await host.Client.GetStringAsync("/api/live-raid/42");
        var doc = JsonDocument.Parse(body).RootElement;

        Assert.False(doc.GetProperty("isOpen").GetBoolean());
        var ids = doc.GetProperty("fightLogIds").EnumerateArray().Select(e => e.GetInt64()).ToList();
        Assert.Equal(new[] { 1L }, ids.ToArray());
    }

    [Fact]
    public async Task GetAggregate_HappyPath_ReturnsAggregateShape()
    {
        using var host = NewHost();
        host.AllowMembership(42L);
        var start = DateTime.UtcNow.AddMinutes(-30);
        await using (var db = await host.DbFactory.CreateDbContextAsync())
        {
            db.FightsReport.Add(new FightsReport { GuildId = 42L, FightsStart = start });
            db.FightLog.AddRange(
                new FightLog { FightLogId = 1, GuildId = 42L, FightType = 1, FightDurationInMs = 60_000, FightStart = start.AddMinutes(2), Url = "u1" },
                new FightLog { FightLogId = 2, GuildId = 42L, FightType = 1, FightDurationInMs = 90_000, FightStart = start.AddMinutes(5), Url = "u2" });
            db.PlayerFightLog.AddRange(
                new PlayerFightLog { PlayerFightLogId = 1, FightLogId = 1, GuildWarsAccountName = "A.1234", CharacterName = "Char", Damage = 100 },
                new PlayerFightLog { PlayerFightLogId = 2, FightLogId = 2, GuildWarsAccountName = "A.1234", CharacterName = "Char", Damage = 200 });
            await db.SaveChangesAsync();
        }

        var body = await host.Client.GetStringAsync("/api/live-raid/42/aggregate");
        var doc = JsonDocument.Parse(body).RootElement;

        Assert.Equal("pve", doc.GetProperty("type").GetString());
        Assert.Equal(2, doc.GetProperty("totalLogs").GetInt32());
        Assert.Equal(2, doc.GetProperty("logs").GetArrayLength());
        Assert.Equal(1, doc.GetProperty("players").GetArrayLength());
        Assert.Equal("A.1234", doc.GetProperty("players")[0].GetProperty("accountName").GetString());
    }

    [Fact]
    public async Task GetAggregate_LogIdsFilter_RestrictsToSubsetWithinWindow()
    {
        using var host = NewHost();
        host.AllowMembership(42L);
        var start = DateTime.UtcNow.AddMinutes(-30);
        await using (var db = await host.DbFactory.CreateDbContextAsync())
        {
            db.FightsReport.Add(new FightsReport { GuildId = 42L, FightsStart = start });
            db.FightLog.AddRange(
                new FightLog { FightLogId = 1, GuildId = 42L, FightType = 1, FightDurationInMs = 60_000, FightStart = start.AddMinutes(2), Url = "u1" },
                new FightLog { FightLogId = 2, GuildId = 42L, FightType = 1, FightDurationInMs = 60_000, FightStart = start.AddMinutes(5), Url = "u2" });
            db.PlayerFightLog.AddRange(
                new PlayerFightLog { PlayerFightLogId = 1, FightLogId = 1, GuildWarsAccountName = "A", CharacterName = "C", Damage = 100 },
                new PlayerFightLog { PlayerFightLogId = 2, FightLogId = 2, GuildWarsAccountName = "A", CharacterName = "C", Damage = 200 });
            await db.SaveChangesAsync();
        }

        var body = await host.Client.GetStringAsync("/api/live-raid/42/aggregate?logIds=1");
        var doc = JsonDocument.Parse(body).RootElement;

        Assert.Equal(1, doc.GetProperty("totalLogs").GetInt32());
        Assert.Equal(1, doc.GetProperty("logs")[0].GetProperty("fightLogId").GetInt64());
    }

    [Fact]
    public async Task GetAggregate_EmptyLogIdsParam_Returns404()
    {
        using var host = NewHost();
        host.AllowMembership(42L);
        var start = DateTime.UtcNow.AddMinutes(-30);
        await using (var db = await host.DbFactory.CreateDbContextAsync())
        {
            db.FightsReport.Add(new FightsReport { GuildId = 42L, FightsStart = start });
            db.FightLog.Add(new FightLog { FightLogId = 1, GuildId = 42L, FightType = 1, FightDurationInMs = 60_000, FightStart = start.AddMinutes(2), Url = "u1" });
            db.PlayerFightLog.Add(new PlayerFightLog { PlayerFightLogId = 1, FightLogId = 1, GuildWarsAccountName = "A", CharacterName = "C", Damage = 100 });
            await db.SaveChangesAsync();
        }

        var response = await host.Client.GetAsync("/api/live-raid/42/aggregate?logIds=");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetAggregate_LogIdsAllOutsideWindow_Returns404()
    {
        using var host = NewHost();
        host.AllowMembership(42L);
        var start = DateTime.UtcNow.AddMinutes(-30);
        await using (var db = await host.DbFactory.CreateDbContextAsync())
        {
            db.FightsReport.Add(new FightsReport { GuildId = 42L, FightsStart = start });
            db.FightLog.Add(new FightLog { FightLogId = 1, GuildId = 42L, FightType = 1, FightDurationInMs = 60_000, FightStart = start.AddMinutes(2), Url = "u1" });
            db.PlayerFightLog.Add(new PlayerFightLog { PlayerFightLogId = 1, FightLogId = 1, GuildWarsAccountName = "A", CharacterName = "C", Damage = 100 });
            db.FightLog.Add(new FightLog { FightLogId = 99, GuildId = 42L, FightType = 1, FightDurationInMs = 60_000, FightStart = start.AddHours(-5), Url = "u99" });
            await db.SaveChangesAsync();
        }

        var response = await host.Client.GetAsync("/api/live-raid/42/aggregate?logIds=99");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
