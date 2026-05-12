using DonBot.Core.Services.RaidLifecycle;
using DonBot.Models.Entities;
using DonBot.Tests.Infrastructure;

namespace DonBot.Tests.Services;

public class RaidLifecycleServiceTests
{
    [Fact]
    public async Task OpenRaidAsync_NoGuildRow_ReturnsGuildNotConfigured()
    {
        using var db = new SqliteTestDb();
        var svc = new RaidLifecycleService(db.Factory);

        var result = await svc.OpenRaidAsync(42L);

        Assert.Equal(RaidOpenOutcome.GuildNotConfigured, result.Outcome);
        Assert.Null(result.Report);
    }

    [Fact]
    public async Task OpenRaidAsync_NoOpenRaid_CreatesOne()
    {
        using var db = new SqliteTestDb();
        await using (var ctx = db.NewContext())
        {
            ctx.Guild.Add(new Guild { GuildId = 42L, GuildName = "Test" });
            await ctx.SaveChangesAsync();
        }
        var svc = new RaidLifecycleService(db.Factory);

        var result = await svc.OpenRaidAsync(42L);

        Assert.Equal(RaidOpenOutcome.Opened, result.Outcome);
        Assert.NotNull(result.Report);
        Assert.Equal(42L, result.Report!.GuildId);
        Assert.Null(result.Report.FightsEnd);

        await using var verify = db.NewContext();
        var stored = await verify.FightsReport.FindAsync(result.Report.FightsReportId);
        Assert.NotNull(stored);
    }

    [Fact]
    public async Task OpenRaidAsync_RaidAlreadyOpen_ReturnsAlreadyOpen()
    {
        using var db = new SqliteTestDb();
        await using (var ctx = db.NewContext())
        {
            ctx.Guild.Add(new Guild { GuildId = 42L, GuildName = "Test" });
            ctx.FightsReport.Add(new FightsReport { GuildId = 42L, FightsStart = DateTime.UtcNow.AddMinutes(-30) });
            await ctx.SaveChangesAsync();
        }
        var svc = new RaidLifecycleService(db.Factory);

        var result = await svc.OpenRaidAsync(42L);

        Assert.Equal(RaidOpenOutcome.AlreadyOpen, result.Outcome);
        Assert.NotNull(result.Report);
    }

    [Fact]
    public async Task OpenRaidAsync_PreviousRaidClosed_OpensNew()
    {
        using var db = new SqliteTestDb();
        await using (var ctx = db.NewContext())
        {
            ctx.Guild.Add(new Guild { GuildId = 42L, GuildName = "Test" });
            ctx.FightsReport.Add(new FightsReport
            {
                GuildId = 42L,
                FightsStart = DateTime.UtcNow.AddHours(-2),
                FightsEnd = DateTime.UtcNow.AddHours(-1)
            });
            await ctx.SaveChangesAsync();
        }
        var svc = new RaidLifecycleService(db.Factory);

        var result = await svc.OpenRaidAsync(42L);

        Assert.Equal(RaidOpenOutcome.Opened, result.Outcome);

        await using var verify = db.NewContext();
        var reportCount = verify.FightsReport.Count(r => r.GuildId == 42L);
        Assert.Equal(2, reportCount);
    }

    [Fact]
    public async Task CloseRaidAsync_NoOpenRaid_ReturnsNoneOpen()
    {
        using var db = new SqliteTestDb();
        var svc = new RaidLifecycleService(db.Factory);

        var result = await svc.CloseRaidAsync(42L);

        Assert.Equal(RaidCloseOutcome.NoneOpen, result.Outcome);
        Assert.Null(result.Report);
    }

    [Fact]
    public async Task CloseRaidAsync_OpenRaid_SetsFightsEnd()
    {
        using var db = new SqliteTestDb();
        long reportId;
        await using (var ctx = db.NewContext())
        {
            var report = new FightsReport { GuildId = 42L, FightsStart = DateTime.UtcNow.AddMinutes(-30) };
            ctx.FightsReport.Add(report);
            await ctx.SaveChangesAsync();
            reportId = report.FightsReportId;
        }
        var svc = new RaidLifecycleService(db.Factory);

        var result = await svc.CloseRaidAsync(42L);

        Assert.Equal(RaidCloseOutcome.Closed, result.Outcome);
        Assert.NotNull(result.Report);
        Assert.NotNull(result.Report!.FightsEnd);
        Assert.Equal(reportId, result.Report.FightsReportId);

        await using var verify = db.NewContext();
        var stored = await verify.FightsReport.FindAsync(reportId);
        Assert.NotNull(stored!.FightsEnd);
    }

    [Fact]
    public async Task GetLatestRaidAsync_ReturnsMostRecentByFightsStart()
    {
        using var db = new SqliteTestDb();
        long latestId;
        await using (var ctx = db.NewContext())
        {
            ctx.FightsReport.Add(new FightsReport { GuildId = 42L, FightsStart = DateTime.UtcNow.AddHours(-3), FightsEnd = DateTime.UtcNow.AddHours(-2) });
            var latest = new FightsReport { GuildId = 42L, FightsStart = DateTime.UtcNow.AddMinutes(-10) };
            ctx.FightsReport.Add(latest);
            ctx.FightsReport.Add(new FightsReport { GuildId = 99L, FightsStart = DateTime.UtcNow.AddMinutes(-1) });
            await ctx.SaveChangesAsync();
            latestId = latest.FightsReportId;
        }
        var svc = new RaidLifecycleService(db.Factory);

        var result = await svc.GetLatestRaidAsync(42L);

        Assert.NotNull(result);
        Assert.Equal(latestId, result!.FightsReportId);
    }

    [Fact]
    public async Task GetLatestRaidAsync_OpenReportPreferredOverNewerClosed()
    {
        using var db = new SqliteTestDb();
        long openId;
        await using (var ctx = db.NewContext())
        {
            var open = new FightsReport { GuildId = 42L, FightsStart = DateTime.UtcNow.AddHours(-3) };
            ctx.FightsReport.Add(open);
            ctx.FightsReport.Add(new FightsReport
            {
                GuildId = 42L,
                FightsStart = DateTime.UtcNow.AddHours(-1),
                FightsEnd = DateTime.UtcNow.AddMinutes(-30)
            });
            await ctx.SaveChangesAsync();
            openId = open.FightsReportId;
        }
        var svc = new RaidLifecycleService(db.Factory);

        var result = await svc.GetLatestRaidAsync(42L);

        Assert.NotNull(result);
        Assert.Equal(openId, result!.FightsReportId);
        Assert.Null(result.FightsEnd);
    }

    [Fact]
    public async Task GetLatestRaidAsync_NoRaids_ReturnsNull()
    {
        using var db = new SqliteTestDb();
        var svc = new RaidLifecycleService(db.Factory);

        var result = await svc.GetLatestRaidAsync(42L);

        Assert.Null(result);
    }
}
