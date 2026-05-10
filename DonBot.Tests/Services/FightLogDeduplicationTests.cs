using DonBot.Models.Entities;
using DonBot.Services;
using DonBot.Tests.Infrastructure;

namespace DonBot.Tests.Services;

public class FightLogDeduplicationTests
{
    private const short FightType = 1;
    private static readonly DateTime FightStart = new(2026, 5, 10, 12, 0, 0, DateTimeKind.Utc);

    private static FightLog MakeFightLog(
        SqliteTestDb db,
        string[] playerNames,
        short fightType = FightType,
        DateTime? fightStart = null)
    {
        using var ctx = db.NewContext();
        var fight = new FightLog
        {
            Url = $"https://b.dps.report/{Guid.NewGuid():N}",
            FightType = fightType,
            FightStart = fightStart ?? FightStart,
            FightDurationInMs = 60_000,
            IsSuccess = true,
            FightPercent = 0
        };
        ctx.FightLog.Add(fight);
        ctx.SaveChanges();

        foreach (var name in playerNames)
        {
            ctx.PlayerFightLog.Add(new PlayerFightLog
            {
                FightLogId = fight.FightLogId,
                GuildWarsAccountName = name,
                CharacterName = name
            });
        }
        ctx.SaveChanges();
        return fight;
    }

    [Fact]
    public async Task FindByContent_NoMatchingFightType_ReturnsNull()
    {
        using var db = new SqliteTestDb();
        MakeFightLog(db, fightType: 1, playerNames: ["A.1234"]);

        using var ctx = db.NewContext();
        var match = await FightLogDeduplication.FindByContentAsync(
            ctx, fightType: 99, FightStart, ["A.1234"]);

        Assert.Null(match);
    }

    [Fact]
    public async Task FindByContent_FightStartOutsideWindow_ReturnsNull()
    {
        using var db = new SqliteTestDb();
        MakeFightLog(db, playerNames: ["A.1234"]);

        using var ctx = db.NewContext();
        var match = await FightLogDeduplication.FindByContentAsync(
            ctx, FightType, FightStart.AddSeconds(10), ["A.1234"]);

        Assert.Null(match);
    }

    [Fact]
    public async Task FindByContent_FightStartWithinTwoSecondWindow_Matches()
    {
        using var db = new SqliteTestDb();
        var existing = MakeFightLog(db, playerNames: ["A.1234"]);

        using var ctx = db.NewContext();
        var match = await FightLogDeduplication.FindByContentAsync(
            ctx, FightType, FightStart.AddSeconds(1), ["A.1234"]);

        Assert.NotNull(match);
        Assert.Equal(existing.FightLogId, match!.FightLogId);
    }

    [Fact]
    public async Task FindByContent_NoExistingPlayers_ReturnsNull()
    {
        // a fight with zero PlayerFightLog rows shouldn't be treated as a content match
        using var db = new SqliteTestDb();
        MakeFightLog(db, playerNames: []);

        using var ctx = db.NewContext();
        var match = await FightLogDeduplication.FindByContentAsync(
            ctx, FightType, FightStart, ["A.1234"]);

        Assert.Null(match);
    }

    [Fact]
    public async Task FindByContent_AllExistingPlayersPresentInNewSet_Matches()
    {
        // dedup rule: every existing player must appear in the new player set
        using var db = new SqliteTestDb();
        var existing = MakeFightLog(db, playerNames: ["A.1234", "B.5678"]);

        using var ctx = db.NewContext();
        var match = await FightLogDeduplication.FindByContentAsync(
            ctx, FightType, FightStart, ["A.1234", "B.5678", "C.9999"]);

        Assert.NotNull(match);
        Assert.Equal(existing.FightLogId, match!.FightLogId);
    }

    [Fact]
    public async Task FindByContent_NewSetMissingExistingPlayer_ReturnsNull()
    {
        using var db = new SqliteTestDb();
        MakeFightLog(db, playerNames: ["A.1234", "B.5678"]);

        using var ctx = db.NewContext();
        var match = await FightLogDeduplication.FindByContentAsync(
            ctx, FightType, FightStart, ["A.1234"]);

        Assert.Null(match);
    }

    [Fact]
    public async Task FindByContent_PlayerNameMatchIsCaseInsensitive()
    {
        using var db = new SqliteTestDb();
        var existing = MakeFightLog(db, playerNames: ["Alice.1234"]);

        using var ctx = db.NewContext();
        var match = await FightLogDeduplication.FindByContentAsync(
            ctx, FightType, FightStart, ["alice.1234"]);

        Assert.NotNull(match);
        Assert.Equal(existing.FightLogId, match!.FightLogId);
    }

    [Fact]
    public async Task FindByContent_ExactMatchOnFightStart_Matches()
    {
        using var db = new SqliteTestDb();
        var existing = MakeFightLog(db, playerNames: ["A.1234"]);

        using var ctx = db.NewContext();
        var match = await FightLogDeduplication.FindByContentAsync(
            ctx, FightType, FightStart, ["A.1234"]);

        Assert.NotNull(match);
        Assert.Equal(existing.FightLogId, match!.FightLogId);
    }

    [Fact]
    public async Task FindByContent_FightStartTwoSecondsBefore_Matches()
    {
        using var db = new SqliteTestDb();
        MakeFightLog(db, playerNames: ["A.1234"]);

        using var ctx = db.NewContext();
        var match = await FightLogDeduplication.FindByContentAsync(
            ctx, FightType, FightStart.AddSeconds(-2), ["A.1234"]);

        Assert.NotNull(match);
    }
}
