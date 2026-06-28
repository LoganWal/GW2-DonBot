using DonBot.Core.Models.Entities;
using DonBot.Core.Models.Enums;
using DonBot.Services.GuildWarsServices;
using DonBot.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace DonBot.Tests.Services.GuildWarsServices;

public class PointsAwardServiceTests
{
    [Fact]
    public async Task AwardFightAsync_RanksEarnedMetricsAndHalvesEachNextComponent()
    {
        using var db = new SqliteTestDb();
        await SeedVerifiedAccountAsync(db, discordId: 100, accountName: "Player.1234");

        await SeedFightAsync(db, 1, FightTypesEnum.Cairn, isSuccess: true, [
            Player(1, 1, "Ref.0001", damage: 6_000, strips: 12)
        ]);
        await SeedFightAsync(db, 2, FightTypesEnum.Cairn, isSuccess: true, [
            Player(2, 2, "Player.1234", damage: 6_000, strips: 24)
        ]);

        var service = NewService(db);
        var awards = await service.AwardFightAsync(2);

        Assert.Equal(2, awards.Count);
        Assert.Equal("strips", awards[0].Metric);
        Assert.Equal(8m, awards[0].Points);
        Assert.Equal("dps", awards[1].Metric);
        Assert.Equal(4m, awards[1].Points);

        using var ctx = db.NewContext();
        var account = await ctx.Account.FindAsync(100L);
        Assert.NotNull(account);
        Assert.Equal(12m, account.Points);
        Assert.Equal(12m, account.AvailablePoints);
    }

    [Fact]
    public async Task AwardFightAsync_HalvesEachEarnedComponentAfterTheFirst()
    {
        using var db = new SqliteTestDb();
        await SeedVerifiedAccountAsync(db, discordId: 100, accountName: "Player.1234");

        await SeedFightAsync(db, 1, FightTypesEnum.Cairn, isSuccess: true, [
            Player(1, 1, "Ref.0001", damage: 6_000, cleanses: 12, strips: 12, stabOn: 10, healing: 600, barrier: 600)
        ]);
        await SeedFightAsync(db, 2, FightTypesEnum.Cairn, isSuccess: true, [
            Player(2, 2, "Player.1234", damage: 6_000, cleanses: 12, strips: 12, stabOn: 10, healing: 600, barrier: 600)
        ]);

        var service = NewService(db);
        var awards = await service.AwardFightAsync(2);

        Assert.Equal(6, awards.Count);
        Assert.Equal([8m, 4m, 2m, 1m, 0.5m, 0.25m], awards.Select(a => a.Points).ToArray());
        Assert.Equal(15.75m, awards.Sum(a => a.Points));
    }

    [Fact]
    public async Task AwardFightAsync_ScalesPointsBelowNinetyFifthPercentile()
    {
        using var db = new SqliteTestDb();
        await SeedVerifiedAccountAsync(db, discordId: 100, accountName: "Player.1234");

        await SeedFightAsync(db, 1, FightTypesEnum.Cairn, isSuccess: true, [
            Player(1, 1, "Ref.0001", damage: 6_000)
        ]);
        await SeedFightAsync(db, 2, FightTypesEnum.Cairn, isSuccess: true, [
            Player(2, 2, "Player.1234", damage: 3_000)
        ]);

        var service = NewService(db);
        var awards = await service.AwardFightAsync(2);

        var award = Assert.Single(awards);
        Assert.Equal("dps", award.Metric);
        Assert.Equal(0.5m, award.Multiplier);
        Assert.Equal(4m, award.Points);
        Assert.Equal("Scaled to 95th percentile", award.Reason);
    }

    [Fact]
    public async Task AwardFightAsync_RanksScaledComponentsByPercentOfBenchmark()
    {
        using var db = new SqliteTestDb();
        await SeedVerifiedAccountAsync(db, discordId: 100, accountName: "Player.1234");

        await SeedFightAsync(db, 1, FightTypesEnum.Cairn, isSuccess: true, [
            Player(1, 1, "Ref.0001", damage: 6_000, strips: 100)
        ]);
        await SeedFightAsync(db, 2, FightTypesEnum.Cairn, isSuccess: true, [
            Player(2, 2, "Player.1234", damage: 3_000, strips: 75)
        ]);

        var service = NewService(db);
        var awards = await service.AwardFightAsync(2);

        Assert.Equal(2, awards.Count);
        Assert.Equal("strips", awards[0].Metric);
        Assert.Equal(0.75m, awards[0].Multiplier);
        Assert.Equal(6m, awards[0].Points);
        Assert.Equal("dps", awards[1].Metric);
        Assert.Equal(0.5m, awards[1].Multiplier);
        Assert.Equal(2m, awards[1].Points);
        Assert.Equal(8m, awards.Sum(a => a.Points));
    }

    [Fact]
    public async Task AwardFightAsync_OnlyUsesCompletedPveFightsAsReferences()
    {
        using var db = new SqliteTestDb();
        await SeedVerifiedAccountAsync(db, discordId: 100, accountName: "Player.1234");

        await SeedFightAsync(db, 1, FightTypesEnum.Cairn, isSuccess: false, [
            Player(1, 1, "FailedRef.0001", damage: 600_000)
        ]);
        await SeedFightAsync(db, 2, FightTypesEnum.Cairn, isSuccess: true, [
            Player(2, 2, "CompletedRef.0001", damage: 6_000)
        ]);
        await SeedFightAsync(db, 3, FightTypesEnum.Cairn, isSuccess: true, [
            Player(3, 3, "Player.1234", damage: 6_000)
        ]);

        var service = NewService(db);
        var awards = await service.AwardFightAsync(3);

        var award = Assert.Single(awards);
        Assert.Equal("dps", award.Metric);
        Assert.Equal(100m, award.PercentileValue);
        Assert.Equal(8m, award.Points);
    }

    [Fact]
    public async Task AwardFightAsync_UsesIncompleteWvWFightsAsReferences()
    {
        using var db = new SqliteTestDb();
        await SeedVerifiedAccountAsync(db, discordId: 100, accountName: "Player.1234");

        await SeedFightAsync(db, 1, FightTypesEnum.WvW, isSuccess: false, [
            Player(1, 1, "Ref.0001", damage: 6_000)
        ]);
        await SeedFightAsync(db, 2, FightTypesEnum.WvW, isSuccess: false, [
            Player(2, 2, "Player.1234", damage: 6_000)
        ]);

        var service = NewService(db);
        var awards = await service.AwardFightAsync(2);

        var award = Assert.Single(awards);
        Assert.Equal("dps", award.Metric);
        Assert.Equal(8m, award.Points);
    }

    [Theory]
    [InlineData(FightTypesEnum.Unkn)]
    [InlineData(FightTypesEnum.Golem)]
    public async Task AwardFightAsync_DoesNotAwardUnknownOrGolemFightTypes(FightTypesEnum fightType)
    {
        using var db = new SqliteTestDb();
        await SeedVerifiedAccountAsync(db, discordId: 100, accountName: "Player.1234");

        await SeedFightAsync(db, 1, fightType, isSuccess: true, [
            Player(1, 1, "Ref.0001", damage: 6_000)
        ]);
        await SeedFightAsync(db, 2, fightType, isSuccess: true, [
            Player(2, 2, "Player.1234", damage: 60_000)
        ]);

        var service = NewService(db);
        var awards = await service.AwardFightAsync(2);

        Assert.Empty(awards);
    }

    [Fact]
    public async Task AwardFightAsync_AwardsStabilityWithTinyBenchmark()
    {
        using var db = new SqliteTestDb();
        await SeedVerifiedAccountAsync(db, discordId: 100, accountName: "Player.1234");

        await SeedFightAsync(db, 1, FightTypesEnum.Cairn, isSuccess: true, [
            Player(1, 1, "Ref.0001", stabOn: 2)
        ]);
        await SeedFightAsync(db, 2, FightTypesEnum.Cairn, isSuccess: true, [
            Player(2, 2, "Player.1234", stabOn: 10)
        ]);

        var service = NewService(db);
        var awards = await service.AwardFightAsync(2);

        var award = Assert.Single(awards);
        Assert.Equal("stability", award.Metric);
        Assert.Equal(1m, award.Multiplier);
        Assert.Equal(8m, award.Points);
        Assert.Equal("95th percentile", award.Reason);
    }

    [Fact]
    public async Task AwardFightAsync_AwardsStabilityWithoutMinimumFloor()
    {
        using var db = new SqliteTestDb();
        await SeedVerifiedAccountAsync(db, discordId: 100, accountName: "Player.1234");

        await SeedFightAsync(db, 1, FightTypesEnum.Cairn, isSuccess: true, [
            Player(1, 1, "Ref.0001", stabOn: 0.25m)
        ]);
        await SeedFightAsync(db, 2, FightTypesEnum.Cairn, isSuccess: true, [
            Player(2, 2, "Player.1234", stabOn: 0.25m)
        ]);

        var service = NewService(db);
        var awards = await service.AwardFightAsync(2);

        var award = Assert.Single(awards);
        Assert.Equal("stability", award.Metric);
        Assert.Equal("Stability", award.MetricLabel);
        Assert.Equal(0.25m, award.PercentileValue);
        Assert.Equal(8m, award.Points);
        Assert.Equal("95th percentile", award.Reason);
    }

    [Fact]
    public async Task AwardFightAsync_GivesFullCreditForIncompletePveFightAtNinetyFifthPercentile()
    {
        using var db = new SqliteTestDb();
        await SeedVerifiedAccountAsync(db, discordId: 100, accountName: "Player.1234");

        await SeedFightAsync(db, 1, FightTypesEnum.Cairn, isSuccess: true, [
            Player(1, 1, "Ref.0001", damage: 6_000)
        ]);
        await SeedFightAsync(db, 2, FightTypesEnum.Cairn, isSuccess: false, [
            Player(2, 2, "Player.1234", damage: 6_000)
        ]);

        var service = NewService(db);
        var awards = await service.AwardFightAsync(2);

        var award = Assert.Single(awards);
        Assert.Equal("dps", award.Metric);
        Assert.Equal(1m, award.Multiplier);
        Assert.Equal(8m, award.Points);
        Assert.Equal("95th percentile", award.Reason);
    }

    [Fact]
    public async Task AwardFightAsync_GivesHalfCreditForIncompletePveFightAboveNinetyNinthPercentile()
    {
        using var db = new SqliteTestDb();
        await SeedVerifiedAccountAsync(db, discordId: 100, accountName: "Player.1234");

        await SeedFightAsync(db, 1, FightTypesEnum.Cairn, isSuccess: true, [
            Player(1, 1, "Ref.0001", damage: 6_000),
            Player(2, 1, "Ref.0002", damage: 12_000),
            Player(3, 1, "Ref.0003", damage: 18_000),
            Player(4, 1, "Ref.0004", damage: 24_000),
            Player(5, 1, "Ref.0005", damage: 30_000)
        ]);
        await SeedFightAsync(db, 2, FightTypesEnum.Cairn, isSuccess: false, [
            Player(6, 2, "Player.1234", damage: 36_000)
        ]);

        var service = NewService(db);
        var awards = await service.AwardFightAsync(2);

        var award = Assert.Single(awards);
        Assert.Equal("dps", award.Metric);
        Assert.Equal(0.5m, award.Multiplier);
        Assert.Equal(4m, award.Points);
        Assert.Equal("Detected anomaly half credit", award.Reason);
    }

    [Fact]
    public async Task AwardFightAsync_GivesFullCreditForIncompletePveFightBetweenNinetyFifthAndNinetyNinthPercentile()
    {
        using var db = new SqliteTestDb();
        await SeedVerifiedAccountAsync(db, discordId: 100, accountName: "Player.1234");

        await SeedFightAsync(db, 1, FightTypesEnum.Cairn, isSuccess: true, [
            Player(1, 1, "Ref.0001", damage: 6_000),
            Player(2, 1, "Ref.0002", damage: 12_000),
            Player(3, 1, "Ref.0003", damage: 18_000),
            Player(4, 1, "Ref.0004", damage: 24_000),
            Player(5, 1, "Ref.0005", damage: 30_000)
        ]);
        await SeedFightAsync(db, 2, FightTypesEnum.Cairn, isSuccess: false, [
            Player(6, 2, "Player.1234", damage: 29_000)
        ]);

        var service = NewService(db);
        var awards = await service.AwardFightAsync(2);

        var award = Assert.Single(awards);
        Assert.Equal("dps", award.Metric);
        Assert.Equal(1m, award.Multiplier);
        Assert.Equal(8m, award.Points);
        Assert.Equal("95th percentile", award.Reason);
    }

    [Fact]
    public async Task AwardFightAsync_DoesNotAwardStripsOrCleansesWhenBenchmarkIsBelowTwelve()
    {
        using var db = new SqliteTestDb();
        await SeedVerifiedAccountAsync(db, discordId: 100, accountName: "Player.1234");

        await SeedFightAsync(db, 1, FightTypesEnum.Cairn, isSuccess: true, [
            Player(1, 1, "Ref.0001", cleanses: 11, strips: 11)
        ]);
        await SeedFightAsync(db, 2, FightTypesEnum.Cairn, isSuccess: true, [
            Player(2, 2, "Player.1234", cleanses: 100, strips: 100)
        ]);

        var service = NewService(db);
        var awards = await service.AwardFightAsync(2);

        Assert.Empty(awards);
    }

    [Fact]
    public async Task AwardFightAsync_DoesNotAwardStripsOrCleansesWhenMostlyZeroBenchmarkIsBelowTwelve()
    {
        using var db = new SqliteTestDb();
        await SeedVerifiedAccountAsync(db, discordId: 100, accountName: "Player.1234");

        var referencePlayers = Enumerable.Range(1, 19)
            .Select(i => Player(i, 1, $"Ref{i}.0001"))
            .Append(Player(20, 1, "HighRef.0001", cleanses: 100, strips: 100))
            .ToList();

        await SeedFightAsync(db, 1, FightTypesEnum.Cairn, isSuccess: true, referencePlayers);
        await SeedFightAsync(db, 2, FightTypesEnum.Cairn, isSuccess: true, [
            Player(21, 2, "Player.1234", cleanses: 100, strips: 100)
        ]);

        var service = NewService(db);
        var awards = await service.AwardFightAsync(2);

        Assert.Empty(awards);
    }

    [Fact]
    public async Task AwardFightAsync_IsIdempotentForAPlayerFight()
    {
        using var db = new SqliteTestDb();
        await SeedVerifiedAccountAsync(db, discordId: 100, accountName: "Player.1234");

        await SeedFightAsync(db, 1, FightTypesEnum.WvW, isSuccess: false, [
            Player(1, 1, "Ref.0001", damage: 6_000)
        ]);
        await SeedFightAsync(db, 2, FightTypesEnum.WvW, isSuccess: false, [
            Player(2, 2, "Player.1234", damage: 12_000)
        ]);

        var service = NewService(db);
        var first = await service.AwardFightAsync(2);
        var second = await service.AwardFightAsync(2);

        Assert.Single(first);
        Assert.Empty(second);

        using var ctx = db.NewContext();
        var account = await ctx.Account.FindAsync(100L);
        Assert.NotNull(account);
        Assert.Equal(8m, account.Points);
        Assert.Equal(8m, account.AvailablePoints);
    }

    [Fact]
    public async Task AwardFightAsync_AddsMissingMetricForAlreadyAwardedPlayerFight()
    {
        using var db = new SqliteTestDb();
        await SeedVerifiedAccountAsync(db, discordId: 100, accountName: "Player.1234");

        await SeedFightAsync(db, 1, FightTypesEnum.Cairn, isSuccess: true, [
            Player(1, 1, "Ref.0001", damage: 6_000, stabOn: 10)
        ]);
        await SeedFightAsync(db, 2, FightTypesEnum.Cairn, isSuccess: true, [
            Player(2, 2, "Player.1234", damage: 6_000, stabOn: 10)
        ]);

        await using (var ctx = db.NewContext())
        {
            var account = await ctx.Account.AsTracking().FirstAsync(a => a.DiscordId == 100L);
            account.Points = 8m;
            account.AvailablePoints = 8m;
            ctx.PlayerPointAward.Add(new PlayerPointAward
            {
                FightLogId = 2,
                PlayerFightLogId = 2,
                DiscordId = 100,
                GuildWarsAccountName = "Player.1234",
                FightType = (short)FightTypesEnum.Cairn,
                Metric = "dps",
                MetricLabel = "DPS",
                MetricValue = 100m,
                PercentileValue = 100m,
                BasePoints = 8m,
                Multiplier = 1m,
                Points = 8m,
                Reason = "95th percentile",
                AwardedAt = DateTime.UtcNow
            });
            await ctx.SaveChangesAsync();
        }

        var service = NewService(db);
        var awards = await service.AwardFightAsync(2);

        var award = Assert.Single(awards);
        Assert.Equal("stability", award.Metric);
        Assert.Equal("Stability", award.MetricLabel);
        Assert.Equal(4m, award.BasePoints);
        Assert.Equal(4m, award.Points);

        await using var verifyCtx = db.NewContext();
        var accountAfter = await verifyCtx.Account.FindAsync(100L);
        Assert.NotNull(accountAfter);
        Assert.Equal(12m, accountAfter.Points);
        Assert.Equal(12m, accountAfter.AvailablePoints);
        Assert.Equal(2, verifyCtx.PlayerPointAward.Count(a => a.PlayerFightLogId == 2));
    }

    private static PointsAwardService NewService(SqliteTestDb db) =>
        new(db.Factory, NullLogger<PointsAwardService>.Instance);

    private static async Task SeedVerifiedAccountAsync(SqliteTestDb db, long discordId, string accountName)
    {
        await using var ctx = db.NewContext();
        ctx.Account.Add(new Account { DiscordId = discordId });
        ctx.GuildWarsAccount.Add(new GuildWarsAccount
        {
            GuildWarsAccountId = Guid.NewGuid(),
            DiscordId = discordId,
            GuildWarsAccountName = accountName
        });
        await ctx.SaveChangesAsync();
    }

    private static async Task SeedFightAsync(
        SqliteTestDb db,
        long fightLogId,
        FightTypesEnum fightType,
        bool isSuccess,
        IReadOnlyList<PlayerFightLog> players)
    {
        await using var ctx = db.NewContext();
        ctx.FightLog.Add(new FightLog
        {
            FightLogId = fightLogId,
            FightType = (short)fightType,
            FightStart = DateTime.UtcNow.AddMinutes(fightLogId),
            FightDurationInMs = 60_000,
            IsSuccess = isSuccess,
            Url = $"https://dps.report/{fightLogId}"
        });
        ctx.PlayerFightLog.AddRange(players);
        await ctx.SaveChangesAsync();
    }

    private static PlayerFightLog Player(
        long playerFightLogId,
        long fightLogId,
        string accountName,
        long damage = 0,
        long cleanses = 0,
        long strips = 0,
        decimal stabOn = 0,
        long healing = 0,
        long barrier = 0) => new()
    {
        PlayerFightLogId = playerFightLogId,
        FightLogId = fightLogId,
        GuildWarsAccountName = accountName,
        CharacterName = accountName,
        Damage = damage,
        Cleanses = cleanses,
        Strips = strips,
        StabGenOnGroup = stabOn,
        Healing = healing,
        BarrierGenerated = barrier
    };
}
