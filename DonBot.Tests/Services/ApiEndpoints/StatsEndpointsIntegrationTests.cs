using System.Net;
using System.Text.Json;
using DonBot.Api.Endpoints;
using DonBot.Core.Models.Entities;
using DonBot.Tests.Infrastructure;

namespace DonBot.Tests.Services.ApiEndpoints;

public class StatsEndpointsIntegrationTests
{
    private static MinimalApiHost NewHost() => new(app => app.MapStatsEndpoints());

    [Fact]
    public async Task GetMyStats_NoAuth_Returns401()
    {
        using var host = NewHost();
        var response = await host.Client.GetAsync("/api/stats/me");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetMyStats_NoFights_ReturnsNullWvwAndPve()
    {
        using var host = NewHost();
        host.AuthenticateAs(123L);

        var body = await host.Client.GetStringAsync("/api/stats/me");
        var doc = JsonDocument.Parse(body);

        Assert.Equal(JsonValueKind.Null, doc.RootElement.GetProperty("wvw").ValueKind);
        Assert.Equal(JsonValueKind.Null, doc.RootElement.GetProperty("pve").ValueKind);
    }

    [Fact]
    public async Task GetMyStats_OnlyWvwFights_PveIsNull()
    {
        using var host = NewHost();
        await SeedSinglePlayerFight(host, fightType: 0, damage: 5000, durationMs: 60_000);
        host.AuthenticateAs(123L);

        var body = await host.Client.GetStringAsync("/api/stats/me");
        var doc = JsonDocument.Parse(body);

        Assert.Equal(JsonValueKind.Null, doc.RootElement.GetProperty("pve").ValueKind);
        var wvw = doc.RootElement.GetProperty("wvw");
        Assert.Equal(1, wvw.GetProperty("totalFights").GetInt32());
        Assert.Equal(5000L, wvw.GetProperty("totalDamage").GetInt64());
    }

    [Fact]
    public async Task GetMyStats_OnlyPveFights_WvwIsNull()
    {
        using var host = NewHost();
        await SeedSinglePlayerFight(host, fightType: 1, damage: 8000, durationMs: 60_000);
        host.AuthenticateAs(123L);

        var body = await host.Client.GetStringAsync("/api/stats/me");
        var doc = JsonDocument.Parse(body);

        Assert.Equal(JsonValueKind.Null, doc.RootElement.GetProperty("wvw").ValueKind);
        var pve = doc.RootElement.GetProperty("pve");
        Assert.Equal(1, pve.GetProperty("totalFights").GetInt32());
        Assert.Equal(8000L, pve.GetProperty("totalDamage").GetInt64());
    }

    [Fact]
    public async Task GetMyStats_ReturnsPveAndWvwPlaystyleBreakdowns()
    {
        using var host = NewHost();
        await using (var db = await host.DbFactory.CreateDbContextAsync())
        {
            db.GuildWarsAccount.Add(new GuildWarsAccount
            {
                GuildWarsAccountId = Guid.NewGuid(),
                DiscordId = 123L,
                GuildWarsAccountName = "Player.1234"
            });
            db.FightLog.AddRange(
                Fight(1, 1),
                Fight(2, 2),
                Fight(3, 3),
                Fight(4, 0),
                Fight(5, 0),
                Fight(6, 0),
                Fight(7, 0));
            db.PlayerFightLog.AddRange(
                Player(1, 1, damage: 60_000),
                Player(2, 2, damage: 30_000, boonRole: "boon-dps"),
                Player(3, 3, damage: 6_000, boonRole: "boon-healer"),
                Player(4, 4, damage: 60_000),
                Player(5, 5, damage: 30_000, cleanses: 12),
                Player(6, 6, damage: 6_000, strips: 6),
                Player(7, 7, damage: 3_000, healing: 60_000));
            await db.SaveChangesAsync();
        }
        host.AuthenticateAs(123L);

        var body = await host.Client.GetStringAsync("/api/stats/me");
        var doc = JsonDocument.Parse(body).RootElement;
        var pveBreakdown = doc.GetProperty("pve").GetProperty("playstyleBreakdown");
        var wvwBreakdown = doc.GetProperty("wvw").GetProperty("playstyleBreakdown");

        Assert.Equal(3, pveBreakdown.GetArrayLength());
        AssertBreakdownRow(pveBreakdown[0], "dps", 1, 33.3);
        AssertBreakdownRow(pveBreakdown[1], "boon-dps", 1, 33.3);
        AssertBreakdownRow(pveBreakdown[2], "boon-healer", 1, 33.3);

        Assert.Equal(4, wvwBreakdown.GetArrayLength());
        AssertBreakdownRow(wvwBreakdown[0], "dps", 1, 25);
        AssertBreakdownRow(wvwBreakdown[1], "support-dps", 1, 25);
        AssertBreakdownRow(wvwBreakdown[2], "support", 1, 25);
        AssertBreakdownRow(wvwBreakdown[3], "heal-support", 1, 25);
    }

    [Fact]
    public async Task GetMyBests_NoFights_ReturnsAllNullSections()
    {
        using var host = NewHost();
        host.AuthenticateAs(123L);

        var body = await host.Client.GetStringAsync("/api/stats/bests");
        var doc = JsonDocument.Parse(body);

        Assert.Equal(JsonValueKind.Null, doc.RootElement.GetProperty("wvw").ValueKind);
        Assert.Equal(JsonValueKind.Null, doc.RootElement.GetProperty("pve").ValueKind);
    }

    [Fact]
    public async Task GetMyBests_PveSuccessfulKills_PopulatesBestTimes()
    {
        using var host = NewHost();
        await using (var db = await host.DbFactory.CreateDbContextAsync())
        {
            db.GuildWarsAccount.Add(new GuildWarsAccount
            {
                GuildWarsAccountId = Guid.NewGuid(),
                DiscordId = 123L,
                GuildWarsAccountName = "Player.1234"
            });
            db.FightLog.AddRange(
                new FightLog { FightLogId = 1, FightType = 5, FightDurationInMs = 90_000, IsSuccess = true, FightStart = DateTime.UtcNow, Url = "u1" },
                new FightLog { FightLogId = 2, FightType = 5, FightDurationInMs = 60_000, IsSuccess = true, FightStart = DateTime.UtcNow, Url = "u2" });
            db.PlayerFightLog.AddRange(
                new PlayerFightLog { PlayerFightLogId = 1, FightLogId = 1, GuildWarsAccountName = "Player.1234", CharacterName = "C", Damage = 1000 },
                new PlayerFightLog { PlayerFightLogId = 2, FightLogId = 2, GuildWarsAccountName = "Player.1234", CharacterName = "C", Damage = 2000 });
            await db.SaveChangesAsync();
        }
        host.AuthenticateAs(123L);

        var body = await host.Client.GetStringAsync("/api/stats/bests");
        var doc = JsonDocument.Parse(body);
        var bestTimes = doc.RootElement.GetProperty("bestTimes");

        Assert.Equal(1, bestTimes.GetArrayLength());
        Assert.Equal(60_000L, bestTimes[0].GetProperty("durationMs").GetInt64());
    }

    [Fact]
    public async Task GetMyProgression_NoFights_ReturnsEmpty()
    {
        using var host = NewHost();
        host.AuthenticateAs(123L);

        var body = await host.Client.GetStringAsync("/api/stats/progression?fightType=0");
        Assert.Equal("[]", body);
    }

    [Fact]
    public async Task GetMyProgression_FilterByFightType_OnlyMatchingReturned()
    {
        using var host = NewHost();
        await using (var db = await host.DbFactory.CreateDbContextAsync())
        {
            db.GuildWarsAccount.Add(new GuildWarsAccount
            {
                GuildWarsAccountId = Guid.NewGuid(),
                DiscordId = 123L,
                GuildWarsAccountName = "Player.1234"
            });
            db.FightLog.AddRange(
                new FightLog { FightLogId = 1, FightType = 0, FightDurationInMs = 60_000, FightStart = DateTime.UtcNow.AddMinutes(-5), Url = "wvw" },
                new FightLog { FightLogId = 2, FightType = 1, FightDurationInMs = 60_000, FightStart = DateTime.UtcNow.AddMinutes(-3), Url = "pve" });
            db.PlayerFightLog.AddRange(
                new PlayerFightLog { PlayerFightLogId = 1, FightLogId = 1, GuildWarsAccountName = "Player.1234", CharacterName = "C", Damage = 100 },
                new PlayerFightLog { PlayerFightLogId = 2, FightLogId = 2, GuildWarsAccountName = "Player.1234", CharacterName = "C", Damage = 200 });
            await db.SaveChangesAsync();
        }
        host.AuthenticateAs(123L);

        var wvwBody = await host.Client.GetStringAsync("/api/stats/progression?fightType=0");
        var pveBody = await host.Client.GetStringAsync("/api/stats/progression?fightType=1");

        Assert.Equal(1, JsonDocument.Parse(wvwBody).RootElement.GetArrayLength());
        Assert.Equal(1, JsonDocument.Parse(pveBody).RootElement.GetArrayLength());
    }

    [Fact]
    public async Task GetMyProgression_PlaystyleFilter_ReturnsOnlySelectedRole()
    {
        using var host = NewHost();
        await using (var db = await host.DbFactory.CreateDbContextAsync())
        {
            db.GuildWarsAccount.Add(new GuildWarsAccount
            {
                GuildWarsAccountId = Guid.NewGuid(),
                DiscordId = 123L,
                GuildWarsAccountName = "Player.1234"
            });
            db.FightLog.AddRange(
                new FightLog { FightLogId = 1, FightType = 1, FightDurationInMs = 60_000, FightStart = DateTime.UtcNow.AddMinutes(-5), Url = "pve1" },
                new FightLog { FightLogId = 2, FightType = 1, FightDurationInMs = 60_000, FightStart = DateTime.UtcNow.AddMinutes(-3), Url = "pve2" });
            db.PlayerFightLog.AddRange(
                new PlayerFightLog
                {
                    PlayerFightLogId = 1,
                    FightLogId = 1,
                    GuildWarsAccountName = "Player.1234",
                    CharacterName = "DpsChar",
                    Playstyle = "dps",
                    Damage = 100
                },
                new PlayerFightLog
                {
                    PlayerFightLogId = 2,
                    FightLogId = 2,
                    GuildWarsAccountName = "Player.1234",
                    CharacterName = "BoonChar",
                    Playstyle = "boon-dps",
                    Damage = 200
                });
            await db.SaveChangesAsync();
        }
        host.AuthenticateAs(123L);

        var body = await host.Client.GetStringAsync("/api/stats/progression?fightType=1&playstyles=boon-dps");
        var data = JsonDocument.Parse(body).RootElement;

        Assert.Equal(1, data.GetArrayLength());
        Assert.Equal(2, data[0].GetProperty("fightLogId").GetInt64());
        Assert.Equal("boon-dps", data[0].GetProperty("playstyle").GetString());
        Assert.Equal("Boon DPS", data[0].GetProperty("playstyleLabel").GetString());
    }

    [Fact]
    public async Task GetMechanicsOverview_NoFights_ReturnsEmpty()
    {
        using var host = NewHost();
        host.AuthenticateAs(123L);

        var body = await host.Client.GetStringAsync("/api/stats/mechanics");
        Assert.Equal("[]", body);
    }

    private static void AssertBreakdownRow(JsonElement row, string key, int count, double percent)
    {
        Assert.Equal(key, row.GetProperty("key").GetString());
        Assert.Equal(count, row.GetProperty("count").GetInt32());
        Assert.Equal(percent, row.GetProperty("percent").GetDouble());
    }

    private static FightLog Fight(long fightLogId, short fightType) => new()
    {
        FightLogId = fightLogId,
        FightType = fightType,
        FightDurationInMs = 60_000,
        FightStart = DateTime.UtcNow.AddMinutes(fightLogId),
        Url = $"u{fightLogId}"
    };

    private static PlayerFightLog Player(
        long playerFightLogId,
        long fightLogId,
        long damage,
        long healing = 0,
        long cleanses = 0,
        long strips = 0,
        string boonRole = "") => new()
    {
        PlayerFightLogId = playerFightLogId,
        FightLogId = fightLogId,
        GuildWarsAccountName = "Player.1234",
        CharacterName = $"Char{playerFightLogId}",
        Damage = damage,
        Healing = healing,
        Cleanses = cleanses,
        Strips = strips,
        BoonRole = boonRole
    };

    private static async Task SeedSinglePlayerFight(MinimalApiHost host, short fightType, long damage, long durationMs)
    {
        await using var db = await host.DbFactory.CreateDbContextAsync();
        db.GuildWarsAccount.Add(new GuildWarsAccount
        {
            GuildWarsAccountId = Guid.NewGuid(),
            DiscordId = 123L,
            GuildWarsAccountName = "Player.1234"
        });
        db.FightLog.Add(new FightLog
        {
            FightLogId = 1,
            FightType = fightType,
            FightDurationInMs = durationMs,
            FightStart = DateTime.UtcNow,
            Url = "u"
        });
        db.PlayerFightLog.Add(new PlayerFightLog
        {
            PlayerFightLogId = 1,
            FightLogId = 1,
            GuildWarsAccountName = "Player.1234",
            CharacterName = "Char",
            Damage = damage
        });
        await db.SaveChangesAsync();
    }
}
