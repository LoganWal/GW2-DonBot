using DonBot.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace DonBot.Services;

public static class FightLogDeduplication
{
    public static async Task<FightLog?> FindByContentAsync(
        DatabaseContext ctx,
        short fightType,
        DateTime fightStart,
        IEnumerable<string> playerAccountNames,
        CancellationToken ct = default)
    {
        var window = TimeSpan.FromSeconds(2);
        var contentMatch = await ctx.FightLog.FirstOrDefaultAsync(f =>
            f.FightType == fightType &&
            f.FightStart >= fightStart - window &&
            f.FightStart <= fightStart + window, ct);

        if (contentMatch == null) return null;

        var existingNames = await ctx.PlayerFightLog
            .Where(p => p.FightLogId == contentMatch.FightLogId)
            .Select(p => p.GuildWarsAccountName)
            .ToListAsync(ct);

        var newNames = playerAccountNames.ToHashSet(StringComparer.OrdinalIgnoreCase);
        return existingNames.Count > 0 && existingNames.All(n => newNames.Contains(n))
            ? contentMatch
            : null;
    }
}
