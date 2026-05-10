using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using DonBot.Api.Endpoints;
using DonBot.Models.Entities;
using DonBot.Tests.Infrastructure;

namespace DonBot.Tests.Services.ApiEndpoints;

public class LogsEndpointsIntegrationTests
{
    private static MinimalApiHost NewHost(HttpMessageHandler? wingmanHandler = null) =>
        new(app => app.MapLogsEndpoints(), httpHandler: wingmanHandler);

    [Fact]
    public async Task GetLogs_NoAuth_Returns401()
    {
        using var host = NewHost();
        var response = await host.Client.GetAsync("/api/logs/");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetLogs_NoLinkedAccount_ReturnsEmptyPage()
    {
        using var host = NewHost();
        host.AuthenticateAs(123L);

        var body = await host.Client.GetStringAsync("/api/logs/");
        var doc = JsonDocument.Parse(body);

        Assert.Equal(0, doc.RootElement.GetProperty("total").GetInt32());
        Assert.Equal(0, doc.RootElement.GetProperty("data").GetArrayLength());
    }

    [Fact]
    public async Task GetLogs_OnlyReturnsLogsThePlayerParticipatedIn()
    {
        using var host = NewHost();
        await SeedAccountAndFights(host, "Mine.1234",
            (1, 0, true, DateTime.UtcNow.AddHours(-1)),
            (2, 1, true, DateTime.UtcNow.AddHours(-2)));
        await using (var db = await host.DbFactory.CreateDbContextAsync())
        {
            // a fight the user did NOT participate in
            db.FightLog.Add(new FightLog { FightLogId = 99, FightType = 0, FightStart = DateTime.UtcNow, Url = "u99" });
            db.PlayerFightLog.Add(new PlayerFightLog
            {
                PlayerFightLogId = 99,
                FightLogId = 99,
                GuildWarsAccountName = "Other.5678",
                CharacterName = "Other"
            });
            await db.SaveChangesAsync();
        }
        host.AuthenticateAs(123L);

        var body = await host.Client.GetStringAsync("/api/logs/");
        var doc = JsonDocument.Parse(body);

        Assert.Equal(2, doc.RootElement.GetProperty("total").GetInt32());
    }

    [Fact]
    public async Task GetLogs_OrderedByFightStartDescending()
    {
        using var host = NewHost();
        await SeedAccountAndFights(host, "Mine.1234",
            (1, 0, true, DateTime.UtcNow.AddHours(-2)),
            (2, 0, true, DateTime.UtcNow.AddHours(-1)));
        host.AuthenticateAs(123L);

        var body = await host.Client.GetStringAsync("/api/logs/");
        var data = JsonDocument.Parse(body).RootElement.GetProperty("data");

        Assert.Equal(2, data[0].GetProperty("fightLogId").GetInt64());
        Assert.Equal(1, data[1].GetProperty("fightLogId").GetInt64());
    }

    [Fact]
    public async Task GetLogs_FightTypeFilter_OnlyMatchingTypesReturned()
    {
        using var host = NewHost();
        await SeedAccountAndFights(host, "Mine.1234",
            (1, 0, true, DateTime.UtcNow),
            (2, 1, true, DateTime.UtcNow),
            (3, 5, true, DateTime.UtcNow));
        host.AuthenticateAs(123L);

        var body = await host.Client.GetStringAsync("/api/logs/?fightTypes=0,5");
        var data = JsonDocument.Parse(body).RootElement.GetProperty("data");

        Assert.Equal(2, data.GetArrayLength());
    }

    [Fact]
    public async Task GetLogs_IsSuccessFalseFilter_OnlyFailuresReturned()
    {
        using var host = NewHost();
        await SeedAccountAndFights(host, "Mine.1234",
            (1, 1, true, DateTime.UtcNow),
            (2, 1, false, DateTime.UtcNow));
        host.AuthenticateAs(123L);

        var body = await host.Client.GetStringAsync("/api/logs/?isSuccess=false");
        var data = JsonDocument.Parse(body).RootElement.GetProperty("data");

        Assert.Equal(1, data.GetArrayLength());
        Assert.Equal(2, data[0].GetProperty("fightLogId").GetInt64());
    }

    [Fact]
    public async Task GetLogs_PagingRespectsPageSizeAndPage()
    {
        using var host = NewHost();
        var fights = Enumerable.Range(1, 5)
            .Select(i => ((long)i, (short)0, true, DateTime.UtcNow.AddMinutes(-i)))
            .ToArray();
        await SeedAccountAndFights(host, "Mine.1234", fights);
        host.AuthenticateAs(123L);

        var page1 = await host.Client.GetStringAsync("/api/logs/?page=1&pageSize=2");
        var page2 = await host.Client.GetStringAsync("/api/logs/?page=2&pageSize=2");

        Assert.Equal(2, JsonDocument.Parse(page1).RootElement.GetProperty("data").GetArrayLength());
        Assert.Equal(2, JsonDocument.Parse(page2).RootElement.GetProperty("data").GetArrayLength());
        Assert.Equal(5, JsonDocument.Parse(page1).RootElement.GetProperty("total").GetInt32());
    }

    [Fact]
    public async Task GetMyCharacters_NoAuth_Returns401()
    {
        using var host = NewHost();
        var response = await host.Client.GetAsync("/api/logs/characters");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetMyCharacters_NoFights_ReturnsEmpty()
    {
        using var host = NewHost();
        host.AuthenticateAs(123L);
        var body = await host.Client.GetStringAsync("/api/logs/characters");
        Assert.Equal("[]", body);
    }

    [Fact]
    public async Task GetMyCharacters_DistinctOrderedByName()
    {
        using var host = NewHost();
        await using (var db = await host.DbFactory.CreateDbContextAsync())
        {
            db.GuildWarsAccount.Add(new GuildWarsAccount
            {
                GuildWarsAccountId = Guid.NewGuid(),
                DiscordId = 123L,
                GuildWarsAccountName = "Mine.1234"
            });
            db.FightLog.AddRange(
                new FightLog { FightLogId = 1, FightStart = DateTime.UtcNow, Url = "u1" },
                new FightLog { FightLogId = 2, FightStart = DateTime.UtcNow, Url = "u2" },
                new FightLog { FightLogId = 3, FightStart = DateTime.UtcNow, Url = "u3" });
            db.PlayerFightLog.AddRange(
                new PlayerFightLog { PlayerFightLogId = 1, FightLogId = 1, GuildWarsAccountName = "Mine.1234", CharacterName = "Charlie" },
                new PlayerFightLog { PlayerFightLogId = 2, FightLogId = 2, GuildWarsAccountName = "Mine.1234", CharacterName = "Alpha" },
                new PlayerFightLog { PlayerFightLogId = 3, FightLogId = 3, GuildWarsAccountName = "Mine.1234", CharacterName = "Alpha" });
            await db.SaveChangesAsync();
        }
        host.AuthenticateAs(123L);

        var body = await host.Client.GetStringAsync("/api/logs/characters");
        var doc = JsonDocument.Parse(body);

        Assert.Equal(2, doc.RootElement.GetArrayLength());
        Assert.Equal("Alpha", doc.RootElement[0].GetString());
        Assert.Equal("Charlie", doc.RootElement[1].GetString());
    }

    [Fact]
    public async Task GetLog_NotFound_Returns404()
    {
        using var host = NewHost();
        host.AuthenticateAs(123L);
        var response = await host.Client.GetAsync("/api/logs/9999");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetLog_ReturnsLogPlayersAndMechanics()
    {
        using var host = NewHost();
        await using (var db = await host.DbFactory.CreateDbContextAsync())
        {
            db.FightLog.Add(new FightLog { FightLogId = 5, FightType = 1, FightStart = DateTime.UtcNow, Url = "u5" });
            db.PlayerFightLog.Add(new PlayerFightLog
            {
                PlayerFightLogId = 1,
                FightLogId = 5,
                GuildWarsAccountName = "P.1234",
                CharacterName = "C"
            });
            db.PlayerFightLogMechanic.Add(new PlayerFightLogMechanic
            {
                PlayerFightLogId = 1,
                MechanicName = "Oils",
                MechanicCount = 3
            });
            await db.SaveChangesAsync();
        }
        host.AuthenticateAs(123L);

        var body = await host.Client.GetStringAsync("/api/logs/5");
        var doc = JsonDocument.Parse(body);

        Assert.Equal(5, doc.RootElement.GetProperty("log").GetProperty("fightLogId").GetInt64());
        Assert.Equal(1, doc.RootElement.GetProperty("players").GetArrayLength());
        Assert.Equal(1, doc.RootElement.GetProperty("mechanics").GetArrayLength());
    }

    [Fact]
    public async Task AggregateLogs_EmptyIds_Returns400()
    {
        using var host = NewHost();
        host.AuthenticateAs(123L);
        var response = await host.Client.PostAsJsonAsync("/api/logs/aggregate", new { LogIds = Array.Empty<long>() });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task AggregateLogs_UnknownIds_Returns404()
    {
        using var host = NewHost();
        host.AuthenticateAs(123L);
        var response = await host.Client.PostAsJsonAsync("/api/logs/aggregate", new { LogIds = new[] { 999L } });
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task AggregateLogs_MajorityWvw_TypeIsWvw()
    {
        using var host = NewHost();
        await using (var db = await host.DbFactory.CreateDbContextAsync())
        {
            var t = DateTime.UtcNow;
            db.FightLog.AddRange(
                new FightLog { FightLogId = 1, FightType = 0, FightDurationInMs = 60_000, FightStart = t, Url = "u1" },
                new FightLog { FightLogId = 2, FightType = 0, FightDurationInMs = 60_000, FightStart = t, Url = "u2" },
                new FightLog { FightLogId = 3, FightType = 1, FightDurationInMs = 60_000, FightStart = t, Url = "u3" });
            db.PlayerFightLog.AddRange(
                new PlayerFightLog { PlayerFightLogId = 1, FightLogId = 1, GuildWarsAccountName = "A", CharacterName = "C", Damage = 100 },
                new PlayerFightLog { PlayerFightLogId = 2, FightLogId = 2, GuildWarsAccountName = "A", CharacterName = "C", Damage = 200 },
                new PlayerFightLog { PlayerFightLogId = 3, FightLogId = 3, GuildWarsAccountName = "A", CharacterName = "C", Damage = 300 });
            await db.SaveChangesAsync();
        }
        host.AuthenticateAs(123L);

        var response = await host.Client.PostAsJsonAsync("/api/logs/aggregate", new { LogIds = new[] { 1L, 2L, 3L } });
        var body = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(body);

        Assert.Equal("wvw", doc.RootElement.GetProperty("type").GetString());
        Assert.Equal(3, doc.RootElement.GetProperty("totalLogs").GetInt32());
    }

    [Fact]
    public async Task AggregateLogs_MajorityPve_TypeIsPve()
    {
        using var host = NewHost();
        await using (var db = await host.DbFactory.CreateDbContextAsync())
        {
            var t = DateTime.UtcNow;
            db.FightLog.AddRange(
                new FightLog { FightLogId = 1, FightType = 1, FightDurationInMs = 60_000, FightStart = t, Url = "u1" },
                new FightLog { FightLogId = 2, FightType = 5, FightDurationInMs = 60_000, FightStart = t, Url = "u2" });
            db.PlayerFightLog.AddRange(
                new PlayerFightLog { PlayerFightLogId = 1, FightLogId = 1, GuildWarsAccountName = "A", CharacterName = "C", Damage = 100 },
                new PlayerFightLog { PlayerFightLogId = 2, FightLogId = 2, GuildWarsAccountName = "A", CharacterName = "C", Damage = 200 });
            await db.SaveChangesAsync();
        }
        host.AuthenticateAs(123L);

        var response = await host.Client.PostAsJsonAsync("/api/logs/aggregate", new { LogIds = new[] { 1L, 2L } });
        var body = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(body);

        Assert.Equal("pve", doc.RootElement.GetProperty("type").GetString());
    }

    [Fact]
    public async Task SubmitToWingman_EmptyIds_Returns400()
    {
        using var host = NewHost();
        host.AuthenticateAs(123L);
        var response = await host.Client.PostAsJsonAsync("/api/logs/wingman", new { LogIds = Array.Empty<long>() });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task SubmitToWingman_NoEligibleLogs_ReturnsZeroSubmitted()
    {
        // Only WvW logs and a PvE log without a dps.report URL
        using var host = NewHost();
        await using (var db = await host.DbFactory.CreateDbContextAsync())
        {
            db.FightLog.AddRange(
                new FightLog { FightLogId = 1, FightType = 0, FightStart = DateTime.UtcNow, Url = "https://wvw.report/abc" },
                new FightLog { FightLogId = 2, FightType = 1, FightStart = DateTime.UtcNow, Url = "https://example.com/abc" });
            await db.SaveChangesAsync();
        }
        host.AuthenticateAs(123L);

        var response = await host.Client.PostAsJsonAsync("/api/logs/wingman", new { LogIds = new[] { 1L, 2L } });
        var body = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(body);

        Assert.Equal(0, doc.RootElement.GetProperty("submitted").GetInt32());
    }

    [Fact]
    public async Task SubmitToWingman_EligiblePveLogs_SubmittedToStubbedHttp()
    {
        var calls = 0;
        var handler = new ApiStubHandler(req =>
        {
            calls++;
            Assert.Contains("gw2wingman.nevermindcreations.de/api/importLogQueued", req.RequestUri!.ToString());
            return new HttpResponseMessage(HttpStatusCode.OK);
        });
        using var host = NewHost(handler);
        await using (var db = await host.DbFactory.CreateDbContextAsync())
        {
            db.FightLog.AddRange(
                new FightLog { FightLogId = 1, FightType = 1, FightStart = DateTime.UtcNow, Url = "https://b.dps.report/abc" },
                new FightLog { FightLogId = 2, FightType = 5, FightStart = DateTime.UtcNow, Url = "https://dps.report/xyz" });
            await db.SaveChangesAsync();
        }
        host.AuthenticateAs(123L);

        var response = await host.Client.PostAsJsonAsync("/api/logs/wingman", new { LogIds = new[] { 1L, 2L } });
        var body = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(body);

        Assert.Equal(2, doc.RootElement.GetProperty("submitted").GetInt32());
        Assert.Equal(2, calls);
    }

    [Fact]
    public async Task SubmitToWingman_HttpFailure_RecordedAsUnsuccessful()
    {
        var handler = new ApiStubHandler(_ => throw new HttpRequestException("boom"));
        using var host = NewHost(handler);
        await using (var db = await host.DbFactory.CreateDbContextAsync())
        {
            db.FightLog.Add(new FightLog { FightLogId = 1, FightType = 1, FightStart = DateTime.UtcNow, Url = "https://b.dps.report/abc" });
            await db.SaveChangesAsync();
        }
        host.AuthenticateAs(123L);

        var response = await host.Client.PostAsJsonAsync("/api/logs/wingman", new { LogIds = new[] { 1L } });
        var body = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(body);

        Assert.Equal(1, doc.RootElement.GetProperty("submitted").GetInt32());
        Assert.False(doc.RootElement.GetProperty("results")[0].GetProperty("success").GetBoolean());
    }

    private static async Task SeedAccountAndFights(
        MinimalApiHost host,
        string accountName,
        params (long FightLogId, short FightType, bool IsSuccess, DateTime FightStart)[] fights)
    {
        await using var db = await host.DbFactory.CreateDbContextAsync();
        db.GuildWarsAccount.Add(new GuildWarsAccount
        {
            GuildWarsAccountId = Guid.NewGuid(),
            DiscordId = 123L,
            GuildWarsAccountName = accountName
        });
        foreach (var f in fights)
        {
            db.FightLog.Add(new FightLog
            {
                FightLogId = f.FightLogId,
                FightType = f.FightType,
                FightStart = f.FightStart,
                IsSuccess = f.IsSuccess,
                Url = $"u{f.FightLogId}"
            });
            db.PlayerFightLog.Add(new PlayerFightLog
            {
                PlayerFightLogId = f.FightLogId,
                FightLogId = f.FightLogId,
                GuildWarsAccountName = accountName,
                CharacterName = "Char"
            });
        }
        await db.SaveChangesAsync();
    }

    private sealed class ApiStubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(respond(request));
    }
}
