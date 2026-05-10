using System.Net;
using System.Text.Json;
using DonBot.Api.Endpoints;
using DonBot.Models.Entities;
using DonBot.Tests.Infrastructure;

namespace DonBot.Tests.Services.ApiEndpoints;

public class LeaderboardEndpointsIntegrationTests
{
    private static MinimalApiHost NewHost() => new(app => app.MapLeaderboardEndpoints());

    [Fact]
    public async Task GetMyGuilds_NoAuth_Returns401()
    {
        using var host = NewHost();
        var response = await host.Client.GetAsync("/api/guilds/mine");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetMyGuilds_NoLinkedAccount_ReturnsEmpty()
    {
        using var host = NewHost();
        host.AuthenticateAs(123L);
        var body = await host.Client.GetStringAsync("/api/guilds/mine");
        Assert.Equal("[]", body);
    }

    [Fact]
    public async Task GetMyGuilds_ReturnsOnlyGuildsWherePlayerHasFought()
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
            db.Guild.AddRange(
                new Guild { GuildId = 100, GuildName = "MyGuild" },
                new Guild { GuildId = 200, GuildName = "OtherGuild" });
            db.FightLog.AddRange(
                new FightLog { FightLogId = 1, GuildId = 100, FightStart = DateTime.UtcNow, Url = "u1" },
                new FightLog { FightLogId = 2, GuildId = 200, FightStart = DateTime.UtcNow, Url = "u2" });
            db.PlayerFightLog.Add(new PlayerFightLog
            {
                PlayerFightLogId = 1,
                FightLogId = 1,
                GuildWarsAccountName = "Player.1234",
                CharacterName = "C"
            });
            await db.SaveChangesAsync();
        }
        host.AuthenticateAs(123L);

        var body = await host.Client.GetStringAsync("/api/guilds/mine");
        var doc = JsonDocument.Parse(body);

        Assert.Equal(1, doc.RootElement.GetArrayLength());
        Assert.Equal("MyGuild", doc.RootElement[0].GetProperty("guildName").GetString());
    }

    [Fact]
    public async Task GetLeaderboard_NoData_ReturnsEmptyWvwAndPveArrays()
    {
        using var host = NewHost();
        host.AuthenticateAs(123L);

        var body = await host.Client.GetStringAsync("/api/guilds/100/leaderboard");
        var doc = JsonDocument.Parse(body);

        Assert.Equal(0, doc.RootElement.GetProperty("wvw").GetArrayLength());
        Assert.Equal(0, doc.RootElement.GetProperty("pve").GetArrayLength());
    }

    [Fact]
    public async Task GetLeaderboard_DefaultDays_IncludesOnlyRecentFights()
    {
        using var host = NewHost();
        await using (var db = await host.DbFactory.CreateDbContextAsync())
        {
            db.FightLog.AddRange(
                new FightLog { FightLogId = 1, GuildId = 100, FightType = 0, FightStart = DateTime.UtcNow.AddDays(-1), FightDurationInMs = 60_000, Url = "u1" },
                new FightLog { FightLogId = 2, GuildId = 100, FightType = 0, FightStart = DateTime.UtcNow.AddDays(-30), FightDurationInMs = 60_000, Url = "u2" });
            db.PlayerFightLog.AddRange(
                new PlayerFightLog { PlayerFightLogId = 1, FightLogId = 1, GuildWarsAccountName = "Recent", CharacterName = "C", Damage = 5000 },
                new PlayerFightLog { PlayerFightLogId = 2, FightLogId = 2, GuildWarsAccountName = "Old", CharacterName = "C", Damage = 9999 });
            await db.SaveChangesAsync();
        }
        host.AuthenticateAs(123L);

        var body = await host.Client.GetStringAsync("/api/guilds/100/leaderboard");
        var doc = JsonDocument.Parse(body);
        var wvw = doc.RootElement.GetProperty("wvw");

        Assert.Equal(1, wvw.GetArrayLength());
        Assert.Equal("Recent", wvw[0].GetProperty("accountName").GetString());
    }

    [Fact]
    public async Task GetLeaderboard_GuildIdMinus1_AggregatesAcrossAllGuilds()
    {
        using var host = NewHost();
        await using (var db = await host.DbFactory.CreateDbContextAsync())
        {
            db.FightLog.AddRange(
                new FightLog { FightLogId = 1, GuildId = 100, FightType = 0, FightStart = DateTime.UtcNow.AddDays(-1), FightDurationInMs = 60_000, Url = "u1" },
                new FightLog { FightLogId = 2, GuildId = 200, FightType = 0, FightStart = DateTime.UtcNow.AddDays(-2), FightDurationInMs = 60_000, Url = "u2" });
            db.PlayerFightLog.AddRange(
                new PlayerFightLog { PlayerFightLogId = 1, FightLogId = 1, GuildWarsAccountName = "A", CharacterName = "C", Damage = 1000 },
                new PlayerFightLog { PlayerFightLogId = 2, FightLogId = 2, GuildWarsAccountName = "B", CharacterName = "C", Damage = 2000 });
            await db.SaveChangesAsync();
        }
        host.AuthenticateAs(123L);

        var body = await host.Client.GetStringAsync("/api/guilds/-1/leaderboard");
        var doc = JsonDocument.Parse(body);

        Assert.Equal(2, doc.RootElement.GetProperty("wvw").GetArrayLength());
    }
}
