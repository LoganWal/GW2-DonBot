using DonBot.Core.Models.Entities;
using DonBot.Core.Models.Enums;
using DonBot.Core.Services.Raffles;
using DonBot.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace DonBot.Tests.Services.Raffles;

public class RaffleServiceTests
{
    [Fact]
    public async Task EnterAsync_UpdatesBidAndDebitsAvailablePointsInOneOperation()
    {
        using var db = new SqliteTestDb();
        await using (var seed = await db.Factory.CreateDbContextAsync())
        {
            seed.Account.Add(new Account { DiscordId = 123, Points = 100, AvailablePoints = 100 });
            seed.GuildWarsAccount.Add(new GuildWarsAccount
            {
                GuildWarsAccountId = Guid.NewGuid(),
                DiscordId = 123,
                GuildWarsAccountName = "Player.1234"
            });
            seed.Raffle.Add(new Raffle
            {
                Id = 7,
                GuildId = 42,
                RaffleType = (int)RaffleTypeEnum.Normal,
                IsActive = true
            });
            seed.PlayerRaffleBid.Add(new PlayerRaffleBid
            {
                RaffleId = 7,
                DiscordId = 123,
                PointsSpent = 20
            });
            await seed.SaveChangesAsync();
        }

        var service = CreateService(db);
        var result = await service.EnterAsync(new RaffleEnterRequest(42, 7, 123, 30));

        Assert.Equal(RaffleOperationStatus.Success, result.Status);
        Assert.Equal(50, result.Bid!.PointsSpent);
        Assert.Equal(70, result.AvailablePoints);

        await using var ctx = await db.Factory.CreateDbContextAsync();
        Assert.Equal(70, (await ctx.Account.SingleAsync()).AvailablePoints);
        Assert.Equal(50, (await ctx.PlayerRaffleBid.SingleAsync()).PointsSpent);
    }

    [Fact]
    public async Task CompleteAsync_PicksWeightedWinnersAndClosesRaffle()
    {
        using var db = new SqliteTestDb();
        await using (var seed = await db.Factory.CreateDbContextAsync())
        {
            seed.Raffle.Add(new Raffle
            {
                Id = 7,
                GuildId = 42,
                RaffleType = (int)RaffleTypeEnum.Event,
                IsActive = true
            });
            seed.PlayerRaffleBid.AddRange(
                new PlayerRaffleBid { RaffleId = 7, DiscordId = 101, PointsSpent = 20 },
                new PlayerRaffleBid { RaffleId = 7, DiscordId = 202, PointsSpent = 80 });
            await seed.SaveChangesAsync();
        }

        var service = CreateService(db, 0.25);
        var result = await service.CompleteAsync(new RaffleCompleteRequest(
            42,
            (int)RaffleTypeEnum.Event,
            WinnersCount: 1));

        Assert.Equal(RaffleOperationStatus.Success, result.Status);
        Assert.Equal(202, Assert.Single(result.Winners).DiscordId);

        await using var ctx = await db.Factory.CreateDbContextAsync();
        Assert.False((await ctx.Raffle.SingleAsync()).IsActive);
        var winningBid = await ctx.PlayerRaffleBid.SingleAsync(b => b.DiscordId == 202);
        var losingBid = await ctx.PlayerRaffleBid.SingleAsync(b => b.DiscordId == 101);
        Assert.True(winningBid.IsWinner);
        Assert.False(losingBid.IsWinner);
    }

    [Fact]
    public async Task CompleteWithAnnouncementAsync_AnnouncementFailure_HappensAfterCompletionIsPersisted()
    {
        using var db = new SqliteTestDb();
        await using (var seed = await db.Factory.CreateDbContextAsync())
        {
            seed.Raffle.Add(new Raffle
            {
                Id = 7,
                GuildId = 42,
                RaffleType = (int)RaffleTypeEnum.Normal,
                IsActive = true
            });
            seed.PlayerRaffleBid.Add(new PlayerRaffleBid
            {
                RaffleId = 7,
                DiscordId = 123,
                PointsSpent = 20
            });
            await seed.SaveChangesAsync();
        }

        var service = CreateService(db);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CompleteWithAnnouncementAsync(
                new RaffleCompleteRequest(
                    42,
                    (int)RaffleTypeEnum.Normal,
                    WinnersCount: 1),
                (_, _, _, _) => throw new InvalidOperationException("send failed")));

        await using var ctx = await db.Factory.CreateDbContextAsync();
        Assert.False((await ctx.Raffle.SingleAsync()).IsActive);
        Assert.True((await ctx.PlayerRaffleBid.SingleAsync()).IsWinner);
    }

    [Fact]
    public async Task ReopenAsync_ActivatesLatestRaffleAndClearsWinnerFlags()
    {
        using var db = new SqliteTestDb();
        await using (var seed = await db.Factory.CreateDbContextAsync())
        {
            seed.Raffle.Add(new Raffle
            {
                Id = 7,
                GuildId = 42,
                RaffleType = (int)RaffleTypeEnum.Normal,
                IsActive = false
            });
            seed.PlayerRaffleBid.Add(new PlayerRaffleBid
            {
                RaffleId = 7,
                DiscordId = 123,
                PointsSpent = 50,
                IsWinner = true
            });
            await seed.SaveChangesAsync();
        }

        var service = CreateService(db);
        var result = await service.ReopenAsync(new RaffleReopenRequest(
            42,
            (int)RaffleTypeEnum.Normal,
            CreatorDiscordId: 999));

        Assert.Equal(RaffleOperationStatus.Success, result.Status);

        await using var ctx = await db.Factory.CreateDbContextAsync();
        Assert.True((await ctx.Raffle.SingleAsync()).IsActive);
        Assert.False((await ctx.PlayerRaffleBid.SingleAsync()).IsWinner);
    }

    [Fact]
    public async Task CreateWithMessageReferenceAsync_MessageFailure_DoesNotPersistRaffle()
    {
        using var db = new SqliteTestDb();
        var service = CreateService(db);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreateWithMessageReferenceAsync(
                new RaffleCreateRequest(
                    42,
                    (int)RaffleTypeEnum.Normal,
                    "Prize",
                    CreatorDiscordId: 123),
                (_, _) => throw new InvalidOperationException("send failed")));

        await using var ctx = await db.Factory.CreateDbContextAsync();
        Assert.Empty(await ctx.Raffle.ToListAsync());
    }

    [Fact]
    public async Task ReopenWithMessageReferenceAsync_MessageFailure_DoesNotActivateOrClearWinners()
    {
        using var db = new SqliteTestDb();
        await using (var seed = await db.Factory.CreateDbContextAsync())
        {
            seed.Raffle.Add(new Raffle
            {
                Id = 7,
                GuildId = 42,
                RaffleType = (int)RaffleTypeEnum.Normal,
                IsActive = false
            });
            seed.PlayerRaffleBid.Add(new PlayerRaffleBid
            {
                RaffleId = 7,
                DiscordId = 123,
                PointsSpent = 50,
                IsWinner = true
            });
            await seed.SaveChangesAsync();
        }

        var service = CreateService(db);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.ReopenWithMessageReferenceAsync(
                new RaffleReopenRequest(
                    42,
                    (int)RaffleTypeEnum.Normal,
                    CreatorDiscordId: 999),
                (_, _) => throw new InvalidOperationException("send failed")));

        await using var ctx = await db.Factory.CreateDbContextAsync();
        Assert.False((await ctx.Raffle.SingleAsync()).IsActive);
        Assert.True((await ctx.PlayerRaffleBid.SingleAsync()).IsWinner);
    }

    [Fact]
    public async Task UpdateAsync_RequiresCreatorAndUpdatesDescription()
    {
        using var db = new SqliteTestDb();
        await using (var seed = await db.Factory.CreateDbContextAsync())
        {
            seed.Raffle.Add(new Raffle
            {
                Id = 7,
                GuildId = 42,
                RaffleType = (int)RaffleTypeEnum.Normal,
                IsActive = true,
                CreatorDiscordId = 123,
                Description = "Old"
            });
            await seed.SaveChangesAsync();
        }

        var service = CreateService(db);
        var denied = await service.UpdateAsync(new RaffleUpdateRequest(42, 7, 999, "New"));
        var updated = await service.UpdateAsync(new RaffleUpdateRequest(42, 7, 123, "  New  "));

        Assert.Equal(RaffleOperationStatus.CreatorMismatch, denied.Status);
        Assert.Equal(RaffleOperationStatus.Success, updated.Status);

        await using var ctx = await db.Factory.CreateDbContextAsync();
        Assert.Equal("New", (await ctx.Raffle.SingleAsync()).Description);
    }

    [Fact]
    public async Task GetRandomEntryContextAsync_ReturnsActiveRaffleErrorBeforeAccountError()
    {
        using var db = new SqliteTestDb();
        var service = CreateService(db);

        var noRaffle = await service.GetRandomEntryContextAsync(
            42,
            (int)RaffleTypeEnum.Normal,
            discordId: 123);

        await using (var seed = await db.Factory.CreateDbContextAsync())
        {
            seed.Raffle.Add(new Raffle
            {
                Id = 7,
                GuildId = 42,
                RaffleType = (int)RaffleTypeEnum.Normal,
                IsActive = true
            });
            await seed.SaveChangesAsync();
        }

        var noAccount = await service.GetRandomEntryContextAsync(
            42,
            (int)RaffleTypeEnum.Normal,
            discordId: 123);

        Assert.Equal(RaffleOperationStatus.RaffleNotFound, noRaffle.Status);
        Assert.Equal(RaffleOperationStatus.AccountNotFound, noAccount.Status);
    }

    private static RaffleService CreateService(SqliteTestDb db, params double[] randomValues) =>
        new(
            db.Factory,
            new RaffleWinnerSelector(new SequenceRaffleRandomSource(randomValues)));

    private sealed class SequenceRaffleRandomSource(params double[] values) : IRaffleRandomSource
    {
        private int _index;

        public double NextDouble() =>
            values.Length == 0 ? 0 : values[Math.Min(_index++, values.Length - 1)];
    }
}
