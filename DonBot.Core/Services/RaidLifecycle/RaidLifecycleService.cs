using System.Data;
using DonBot.Models.Entities;
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

        // Serializable transaction so two concurrent callers (Discord slash command + web)
        // cannot both insert an open FightsReport for the same guild. On Postgres the
        // loser sees a serialization-failure exception; we retry once which will then
        // observe the just-inserted row and return AlreadyOpen. SQLite serializes
        // writes via the connection lock so this is effectively a no-op in tests.
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

        // Prefer an open report (matches OpenRaidAsync's "already open" predicate so the
        // live view and the slash command always agree on which raid is current). Fall
        // back to the most recent closed report, with FightsReportId as a stable tiebreak.
        return await ctx.FightsReport
            .Where(r => r.GuildId == guildId)
            .OrderBy(r => r.FightsEnd == null ? 0 : 1)
            .ThenByDescending(r => r.FightsStart)
            .ThenByDescending(r => r.FightsReportId)
            .FirstOrDefaultAsync(ct);
    }
}
