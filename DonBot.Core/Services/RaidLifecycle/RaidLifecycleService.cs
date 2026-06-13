using System.Data;
using DonBot.Core.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace DonBot.Core.Services.RaidLifecycle;

public sealed class RaidLifecycleService(IDbContextFactory<DatabaseContext> dbContextFactory) : IRaidLifecycleService
{
    public async Task<RaidOpenResult> OpenRaidAsync(long guildId, CancellationToken ct = default)
    {
        await using var ctx = await dbContextFactory.CreateDbContextAsync(ct);

        var guild = await ctx.Guild.FirstOrDefaultAsync(g => g.GuildId == guildId, ct);
        if (guild == null)
        {
            return new RaidOpenResult(RaidOpenOutcome.GuildNotConfigured, null);
        }

        // Serializable isolation prevents two callers from opening the same guild raid.
        // A conflicting insert is retried once so the new row can be observed.
        for (var attempt = 0; ; attempt++)
        {
            await using var tx = await ctx.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);

            var existingOpen = await ctx.FightsReport
                .FirstOrDefaultAsync(r => r.GuildId == guildId && r.FightsEnd == null, ct);
            if (existingOpen != null)
            {
                await tx.CommitAsync(ct);
                return new RaidOpenResult(RaidOpenOutcome.AlreadyOpen, existingOpen);
            }

            var report = new FightsReport
            {
                GuildId = guildId,
                FightsStart = DateTime.UtcNow
            };
            ctx.FightsReport.Add(report);

            try
            {
                await ctx.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);
                return new RaidOpenResult(RaidOpenOutcome.Opened, report);
            }
            catch (DbUpdateException) when (attempt == 0)
            {
                ctx.Entry(report).State = EntityState.Detached;
                await tx.RollbackAsync(ct);
            }
        }
    }

    public async Task<RaidCloseResult> CloseRaidAsync(long guildId, CancellationToken ct = default)
    {
        await using var ctx = await dbContextFactory.CreateDbContextAsync(ct);

        var open = await ctx.FightsReport.AsTracking()
            .FirstOrDefaultAsync(r => r.GuildId == guildId && r.FightsEnd == null, ct);
        if (open == null)
        {
            return new RaidCloseResult(RaidCloseOutcome.NoneOpen, null);
        }

        open.FightsEnd = DateTime.UtcNow;
        await ctx.SaveChangesAsync(ct);

        return new RaidCloseResult(RaidCloseOutcome.Closed, open);
    }

    public async Task<FightsReport?> GetLatestRaidAsync(long guildId, CancellationToken ct = default)
    {
        await using var ctx = await dbContextFactory.CreateDbContextAsync(ct);

        // Open raids win over later closed reports.
        return await ctx.FightsReport
            .Where(r => r.GuildId == guildId)
            .OrderBy(r => r.FightsEnd == null ? 0 : 1)
            .ThenByDescending(r => r.FightsStart)
            .ThenByDescending(r => r.FightsReportId)
            .FirstOrDefaultAsync(ct);
    }
}
