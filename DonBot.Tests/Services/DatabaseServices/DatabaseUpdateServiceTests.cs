using DonBot.Models.Entities;
using DonBot.Services.DatabaseServices;
using DonBot.Tests.Infrastructure;

namespace DonBot.Tests.Services.DatabaseServices;

public class DatabaseUpdateServiceTests
{
    private static DatabaseUpdateService<Guild> NewService(SqliteTestDb db) => new(db.Factory);

    [Fact]
    public async Task AddAsync_PersistsEntity()
    {
        using var db = new SqliteTestDb();
        var svc = NewService(db);

        await svc.AddAsync(new Guild { GuildId = 1L, GuildName = "Test" });

        using var ctx = db.NewContext();
        var stored = ctx.Guild.Single();
        Assert.Equal("Test", stored.GuildName);
    }

    [Fact]
    public async Task AddRangeAsync_PersistsAllEntities()
    {
        using var db = new SqliteTestDb();
        var svc = NewService(db);

        await svc.AddRangeAsync([
            new Guild { GuildId = 1L, GuildName = "A" },
            new Guild { GuildId = 2L, GuildName = "B" },
            new Guild { GuildId = 3L, GuildName = "C" }
        ]);

        using var ctx = db.NewContext();
        Assert.Equal(3, ctx.Guild.Count());
    }

    [Fact]
    public async Task GetAllAsync_ReturnsAllRows()
    {
        using var db = new SqliteTestDb();
        var svc = NewService(db);
        await svc.AddRangeAsync([
            new Guild { GuildId = 1L, GuildName = "A" },
            new Guild { GuildId = 2L, GuildName = "B" }
        ]);

        var all = await svc.GetAllAsync();

        Assert.Equal(2, all.Count);
    }

    [Fact]
    public async Task GetWhereAsync_FiltersByPredicate()
    {
        using var db = new SqliteTestDb();
        var svc = NewService(db);
        await svc.AddRangeAsync([
            new Guild { GuildId = 1L, GuildName = "Match" },
            new Guild { GuildId = 2L, GuildName = "Other" },
            new Guild { GuildId = 3L, GuildName = "Match" }
        ]);

        var matches = await svc.GetWhereAsync(g => g.GuildName == "Match");

        Assert.Equal(2, matches.Count);
    }

    [Fact]
    public async Task GetFirstOrDefaultAsync_ReturnsMatch()
    {
        using var db = new SqliteTestDb();
        var svc = NewService(db);
        await svc.AddRangeAsync([
            new Guild { GuildId = 1L, GuildName = "A" },
            new Guild { GuildId = 2L, GuildName = "B" }
        ]);

        var match = await svc.GetFirstOrDefaultAsync(g => g.GuildName == "B");

        Assert.NotNull(match);
        Assert.Equal(2L, match!.GuildId);
    }

    [Fact]
    public async Task GetFirstOrDefaultAsync_NoMatch_ReturnsNull()
    {
        using var db = new SqliteTestDb();
        var svc = NewService(db);

        var match = await svc.GetFirstOrDefaultAsync(g => g.GuildName == "Missing");

        Assert.Null(match);
    }

    [Fact]
    public async Task IfAnyAsync_TrueWhenMatchExists()
    {
        using var db = new SqliteTestDb();
        var svc = NewService(db);
        await svc.AddAsync(new Guild { GuildId = 1L, GuildName = "X" });

        Assert.True(await svc.IfAnyAsync(g => g.GuildName == "X"));
        Assert.False(await svc.IfAnyAsync(g => g.GuildName == "Y"));
    }

    [Fact]
    public async Task UpdateAsync_PersistsChanges()
    {
        using var db = new SqliteTestDb();
        var svc = NewService(db);
        await svc.AddAsync(new Guild { GuildId = 1L, GuildName = "Old" });

        var stored = await svc.GetFirstOrDefaultAsync(g => g.GuildId == 1L);
        stored!.GuildName = "New";
        await svc.UpdateAsync(stored);

        using var ctx = db.NewContext();
        Assert.Equal("New", ctx.Guild.Single().GuildName);
    }

    [Fact]
    public async Task UpdateRangeAsync_PersistsMultipleChanges()
    {
        using var db = new SqliteTestDb();
        var svc = NewService(db);
        await svc.AddRangeAsync([
            new Guild { GuildId = 1L, GuildName = "Old1" },
            new Guild { GuildId = 2L, GuildName = "Old2" }
        ]);

        var all = await svc.GetAllAsync();
        foreach (var g in all) {
            g.GuildName = "Updated";
        }
        await svc.UpdateRangeAsync(all);

        using var ctx = db.NewContext();
        Assert.All(ctx.Guild.ToList(), g => Assert.Equal("Updated", g.GuildName));
    }

    [Fact]
    public async Task DeleteAsync_RemovesEntity()
    {
        using var db = new SqliteTestDb();
        var svc = NewService(db);
        await svc.AddAsync(new Guild { GuildId = 1L, GuildName = "X" });

        var stored = await svc.GetFirstOrDefaultAsync(g => g.GuildId == 1L);
        await svc.DeleteAsync(stored!);

        Assert.Empty(await svc.GetAllAsync());
    }

    [Fact]
    public async Task DeleteRangeAsync_RemovesMultiple()
    {
        using var db = new SqliteTestDb();
        var svc = NewService(db);
        await svc.AddRangeAsync([
            new Guild { GuildId = 1L },
            new Guild { GuildId = 2L },
            new Guild { GuildId = 3L }
        ]);

        var all = await svc.GetAllAsync();
        await svc.DeleteRangeAsync([all[0], all[2]]);

        var remaining = await svc.GetAllAsync();
        Assert.Single(remaining);
        Assert.Equal(2L, remaining[0].GuildId);
    }
}
