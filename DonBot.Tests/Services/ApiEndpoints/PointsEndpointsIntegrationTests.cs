using System.Net;
using System.Text.Json;
using DonBot.Api.Endpoints;
using DonBot.Models.Entities;
using DonBot.Tests.Infrastructure;

namespace DonBot.Tests.Services.ApiEndpoints;

public class PointsEndpointsIntegrationTests
{
    private static MinimalApiHost NewHost() => new(app => app.MapPointsEndpoints());

    [Fact]
    public async Task GetMyPoints_NoAuth_Returns401()
    {
        using var host = NewHost();
        var response = await host.Client.GetAsync("/api/points/me");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetMyPoints_NoAccount_Returns404()
    {
        using var host = NewHost();
        host.AuthenticateAs(123L);
        var response = await host.Client.GetAsync("/api/points/me");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetMyPoints_WithAccount_ReturnsAccount()
    {
        using var host = NewHost();
        await using (var db = await host.DbFactory.CreateDbContextAsync())
        {
            db.Account.Add(new Account { DiscordId = 123L, Points = 500m });
            await db.SaveChangesAsync();
        }
        host.AuthenticateAs(123L);

        var body = await host.Client.GetStringAsync("/api/points/me");

        Assert.Contains("\"discordId\":123", body);
        Assert.Contains("500", body);
    }

    [Fact]
    public async Task GetRaffles_NoAuth_Returns401()
    {
        using var host = NewHost();
        var response = await host.Client.GetAsync("/api/raffles");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetRaffles_NoActiveRaffles_ReturnsEmptyArrays()
    {
        using var host = NewHost();
        host.AuthenticateAs(123L);

        var body = await host.Client.GetStringAsync("/api/raffles");
        var doc = JsonDocument.Parse(body);

        Assert.Equal(0, doc.RootElement.GetProperty("raffles").GetArrayLength());
        Assert.Equal(0, doc.RootElement.GetProperty("userBids").GetArrayLength());
    }

    [Fact]
    public async Task GetRaffles_OnlyReturnsActiveRaffles_AndOnlyOwnBids()
    {
        using var host = NewHost();
        await using (var db = await host.DbFactory.CreateDbContextAsync())
        {
            db.Raffle.AddRange(
                new Raffle { Id = 1, IsActive = true, GuildId = 1, RaffleType = 0 },
                new Raffle { Id = 2, IsActive = false, GuildId = 1, RaffleType = 0 });
            db.PlayerRaffleBid.AddRange(
                new PlayerRaffleBid { RaffleId = 1, DiscordId = 123L, PointsSpent = 100m },
                new PlayerRaffleBid { RaffleId = 1, DiscordId = 999L, PointsSpent = 50m });
            await db.SaveChangesAsync();
        }
        host.AuthenticateAs(123L);

        var body = await host.Client.GetStringAsync("/api/raffles");
        var doc = JsonDocument.Parse(body);

        Assert.Equal(1, doc.RootElement.GetProperty("raffles").GetArrayLength());
        Assert.Equal(1, doc.RootElement.GetProperty("raffles")[0].GetProperty("id").GetInt64());
        Assert.Equal(1, doc.RootElement.GetProperty("userBids").GetArrayLength());
        Assert.Equal(123L, doc.RootElement.GetProperty("userBids")[0].GetProperty("discordId").GetInt64());
    }

    [Fact]
    public async Task GetDashboard_NoAuth_Returns401()
    {
        using var host = NewHost();
        var response = await host.Client.GetAsync("/api/dashboard");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetDashboard_NoLinkedGw2Account_ReturnsNullFightsAndLastFightDate()
    {
        using var host = NewHost();
        await using (var db = await host.DbFactory.CreateDbContextAsync())
        {
            db.Account.Add(new Account { DiscordId = 123L });
            await db.SaveChangesAsync();
        }
        host.AuthenticateAs(123L);

        var body = await host.Client.GetStringAsync("/api/dashboard");
        var doc = JsonDocument.Parse(body);

        Assert.Equal(JsonValueKind.Null, doc.RootElement.GetProperty("fights").ValueKind);
        Assert.Equal(JsonValueKind.Null, doc.RootElement.GetProperty("lastFightDate").ValueKind);
    }

    [Fact]
    public async Task GetDashboard_WithFights_ReturnsAggregateTotals()
    {
        using var host = NewHost();
        await using (var db = await host.DbFactory.CreateDbContextAsync())
        {
            db.Account.Add(new Account { DiscordId = 123L });
            db.GuildWarsAccount.Add(new GuildWarsAccount
            {
                GuildWarsAccountId = Guid.NewGuid(),
                DiscordId = 123L,
                GuildWarsAccountName = "Player.1234"
            });
            db.FightLog.AddRange(
                new FightLog { FightLogId = 1, FightType = 0, FightDurationInMs = 60_000, FightStart = DateTime.UtcNow, Url = "u1" },
                new FightLog { FightLogId = 2, FightType = 1, FightDurationInMs = 60_000, FightStart = DateTime.UtcNow, Url = "u2" });
            db.PlayerFightLog.AddRange(
                new PlayerFightLog { PlayerFightLogId = 1, FightLogId = 1, GuildWarsAccountName = "Player.1234", CharacterName = "C1", Damage = 1000, Kills = 5, Deaths = 1 },
                new PlayerFightLog { PlayerFightLogId = 2, FightLogId = 2, GuildWarsAccountName = "Player.1234", CharacterName = "C2", Damage = 2000, Healing = 500 });
            await db.SaveChangesAsync();
        }
        host.AuthenticateAs(123L);

        var body = await host.Client.GetStringAsync("/api/dashboard");
        var doc = JsonDocument.Parse(body);
        var fights = doc.RootElement.GetProperty("fights");

        Assert.Equal(2, fights.GetProperty("total").GetInt32());
        Assert.Equal(1, fights.GetProperty("wvw").GetInt32());
        Assert.Equal(1, fights.GetProperty("pve").GetInt32());
        Assert.Equal(3000, fights.GetProperty("totalDamage").GetInt64());
        Assert.Equal(5, fights.GetProperty("totalKills").GetInt64()); // wvw-only kills
        Assert.Equal(1, fights.GetProperty("totalDeaths").GetInt64());
    }
}
