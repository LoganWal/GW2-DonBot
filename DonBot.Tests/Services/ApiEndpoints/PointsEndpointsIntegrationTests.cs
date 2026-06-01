using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using DonBot.Api.Endpoints;
using DonBot.Api.Services;
using DonBot.Models.Entities;
using DonBot.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DonBot.Tests.Services.ApiEndpoints;

public class PointsEndpointsIntegrationTests
{
    private const long TestDiscordId = 123L;

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
            => Task.FromResult(false);
    }

    private sealed class FakeCommandAccessService : IDiscordCommandAccessService
    {
        public HashSet<(ulong GuildId, string CommandName)> Allowed { get; } = new();

        public Task<bool> HasCommandAccessAsync(ClaimsPrincipal user, ulong guildId, string commandName, CancellationToken ct = default)
            => Task.FromResult(Allowed.Contains((guildId, commandName)));
    }

    private sealed class RaffleHost : IDisposable
    {
        public MinimalApiHost Inner { get; }
        public FakeUserGuilds Membership { get; } = new();
        public FakeCommandAccessService CommandAccess { get; } = new();

        public RaffleHost()
        {
            var membership = Membership;
            var commandAccess = CommandAccess;
            Inner = NewHost(services =>
            {
                services.AddSingleton<IUserGuildsService>(membership);
                services.AddSingleton<IDiscordCommandAccessService>(commandAccess);
                services.AddSingleton<IRaffleEventHub, RaffleEventHub>();
            });
            Inner.AuthenticateAs(TestDiscordId);
        }

        public IDbContextFactory<DatabaseContext> DbFactory => Inner.DbFactory;
        public HttpClient Client => Inner.Client;

        public void AllowMembership(params long[] guildIds)
        {
            foreach (var id in guildIds)
            {
                Membership.Allowed.Add(id);
            }
        }

        public void AllowCommand(long guildId, string commandName)
        {
            CommandAccess.Allowed.Add(((ulong)guildId, commandName));
        }

        public void Dispose() => Inner.Dispose();
    }

    private static MinimalApiHost NewHost(Action<IServiceCollection>? configureServices = null) =>
        new(app => app.MapPointsEndpoints(), configureServices);

    private static RaffleHost NewRaffleHost() => new();

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
        host.AuthenticateAs(TestDiscordId);

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
        host.AuthenticateAs(TestDiscordId);

        var body = await host.Client.GetStringAsync("/api/raffles");
        var doc = JsonDocument.Parse(body);

        Assert.Equal(1, doc.RootElement.GetProperty("raffles").GetArrayLength());
        Assert.Equal(1, doc.RootElement.GetProperty("raffles")[0].GetProperty("id").GetInt64());
        Assert.Equal(1, doc.RootElement.GetProperty("userBids").GetArrayLength());
        Assert.Equal(123L, doc.RootElement.GetProperty("userBids")[0].GetProperty("discordId").GetInt64());
    }

    [Fact]
    public async Task ListRaffleGuilds_ReturnsOnlyTrackedMemberGuilds()
    {
        using var host = NewRaffleHost();
        host.AllowMembership(1L, 3L);
        await using (var db = await host.DbFactory.CreateDbContextAsync())
        {
            db.Guild.Add(new Guild { GuildId = 1L, GuildName = "Alpha" });
            db.Guild.Add(new Guild { GuildId = 2L, GuildName = "Beta" });
            db.Guild.Add(new Guild { GuildId = 3L, GuildName = "Charlie" });
            await db.SaveChangesAsync();
        }

        var body = await host.Client.GetStringAsync("/api/raffles/guilds");
        var arr = JsonDocument.Parse(body).RootElement;

        Assert.Equal(2, arr.GetArrayLength());
        Assert.Equal("1", arr[0].GetProperty("guildId").GetString());
        Assert.Equal("Alpha", arr[0].GetProperty("guildName").GetString());
        Assert.Equal("3", arr[1].GetProperty("guildId").GetString());
    }

    [Fact]
    public async Task GetRaffleState_ReturnsPermissionFlagsAndTopFiveBidders()
    {
        using var host = NewRaffleHost();
        host.AllowMembership(42L);
        host.AllowCommand(42L, "enter_raffle");
        host.AllowCommand(42L, "create_raffle");
        host.AllowCommand(42L, "complete_raffle");
        await using (var db = await host.DbFactory.CreateDbContextAsync())
        {
            db.Guild.Add(new Guild { GuildId = 42L, GuildName = "Raid Night" });
            db.Account.Add(new Account { DiscordId = TestDiscordId, Points = 1000m, AvailablePoints = 900m });
            db.Raffle.Add(new Raffle
            {
                Id = 10,
                GuildId = 42L,
                IsActive = true,
                RaffleType = 0,
                Description = "Prize",
                CreatorDiscordId = TestDiscordId
            });
            db.Raffle.Add(new Raffle
            {
                Id = 11,
                GuildId = 42L,
                IsActive = false,
                RaffleType = 1,
                Description = "Old event"
            });
            db.PlayerRaffleBid.AddRange(
                new PlayerRaffleBid { RaffleId = 10, DiscordId = TestDiscordId, PointsSpent = 10m },
                new PlayerRaffleBid { RaffleId = 10, DiscordId = 201L, PointsSpent = 600m },
                new PlayerRaffleBid { RaffleId = 10, DiscordId = 202L, PointsSpent = 500m },
                new PlayerRaffleBid { RaffleId = 10, DiscordId = 203L, PointsSpent = 400m },
                new PlayerRaffleBid { RaffleId = 10, DiscordId = 204L, PointsSpent = 300m },
                new PlayerRaffleBid { RaffleId = 10, DiscordId = 205L, PointsSpent = 200m },
                new PlayerRaffleBid { RaffleId = 10, DiscordId = 206L, PointsSpent = 100m });
            db.GuildWarsAccount.Add(new GuildWarsAccount
            {
                GuildWarsAccountId = Guid.NewGuid(),
                DiscordId = 201L,
                GuildWarsAccountName = "Top.0001"
            });
            await db.SaveChangesAsync();
        }

        var body = await host.Client.GetStringAsync("/api/raffles/42");
        var doc = JsonDocument.Parse(body).RootElement;
        var raffle = doc.GetProperty("raffles")[0];
        var permissions = doc.GetProperty("permissions");
        var availability = doc.GetProperty("availability");
        var top = raffle.GetProperty("topBidders");

        Assert.Equal("42", doc.GetProperty("guildId").GetString());
        Assert.Equal("Raid Night", doc.GetProperty("guildName").GetString());
        Assert.True(permissions.GetProperty("canEnterRaffle").GetBoolean());
        Assert.False(permissions.GetProperty("canEnterEventRaffle").GetBoolean());
        Assert.True(permissions.GetProperty("canCreateRaffle").GetBoolean());
        Assert.False(permissions.GetProperty("canCreateEventRaffle").GetBoolean());
        Assert.True(permissions.GetProperty("canCompleteRaffle").GetBoolean());
        Assert.False(availability.GetProperty("hasPreviousRaffle").GetBoolean());
        Assert.True(availability.GetProperty("hasPreviousEventRaffle").GetBoolean());
        Assert.True(raffle.GetProperty("canEdit").GetBoolean());
        Assert.Equal(10m, raffle.GetProperty("userBid").GetDecimal());
        Assert.Equal(5, top.GetArrayLength());
        Assert.Equal("201", top[0].GetProperty("discordId").GetString());
        Assert.Equal("Top.0001", top[0].GetProperty("displayName").GetString());
    }

    [Fact]
    public async Task EnterRaffle_WithCommandAccessSpendsPointsAndUpdatesBid()
    {
        using var host = NewRaffleHost();
        host.AllowMembership(42L);
        host.AllowCommand(42L, "enter_raffle");
        await using (var db = await host.DbFactory.CreateDbContextAsync())
        {
            db.Account.Add(new Account { DiscordId = TestDiscordId, Points = 100m, AvailablePoints = 100m });
            db.GuildWarsAccount.Add(new GuildWarsAccount
            {
                GuildWarsAccountId = Guid.NewGuid(),
                DiscordId = TestDiscordId,
                GuildWarsAccountName = "Player.1234"
            });
            db.Raffle.Add(new Raffle { Id = 7, GuildId = 42L, IsActive = true, RaffleType = 0 });
            db.PlayerRaffleBid.Add(new PlayerRaffleBid { RaffleId = 7, DiscordId = TestDiscordId, PointsSpent = 20m });
            await db.SaveChangesAsync();
        }

        var response = await host.Client.PostAsJsonAsync("/api/raffles/42/enter", new { raffleId = 7, points = 30 });
        var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(50m, doc.GetProperty("pointsSpent").GetDecimal());
        Assert.Equal(70m, doc.GetProperty("availablePoints").GetDecimal());

        await using var verifyDb = await host.DbFactory.CreateDbContextAsync();
        var account = await verifyDb.Account.FindAsync(TestDiscordId);
        var bid = await verifyDb.PlayerRaffleBid.FindAsync(7, TestDiscordId);
        Assert.Equal(70m, account!.AvailablePoints);
        Assert.Equal(50m, bid!.PointsSpent);
    }

    [Fact]
    public async Task EnterRaffle_WithoutCommandAccessDoesNotSpendPoints()
    {
        using var host = NewRaffleHost();
        host.AllowMembership(42L);
        await using (var db = await host.DbFactory.CreateDbContextAsync())
        {
            db.Account.Add(new Account { DiscordId = TestDiscordId, Points = 100m, AvailablePoints = 100m });
            db.GuildWarsAccount.Add(new GuildWarsAccount
            {
                GuildWarsAccountId = Guid.NewGuid(),
                DiscordId = TestDiscordId,
                GuildWarsAccountName = "Player.1234"
            });
            db.Raffle.Add(new Raffle { Id = 7, GuildId = 42L, IsActive = true, RaffleType = 0 });
            await db.SaveChangesAsync();
        }

        var response = await host.Client.PostAsJsonAsync("/api/raffles/42/enter", new { raffleId = 7, points = 30 });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        await using var verifyDb = await host.DbFactory.CreateDbContextAsync();
        var account = await verifyDb.Account.FindAsync(TestDiscordId);
        var bid = await verifyDb.PlayerRaffleBid.FindAsync(7, TestDiscordId);
        Assert.Equal(100m, account!.AvailablePoints);
        Assert.Null(bid);
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
