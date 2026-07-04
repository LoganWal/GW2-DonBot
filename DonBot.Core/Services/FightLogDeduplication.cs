using DonBot.Core.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace DonBot.Core.Services;

public static class FightLogDeduplication
{
    public static async Task<FightLog?> FindByContentAsync(
        DatabaseContext ctx,
        short fightType,
        DateTime fightStart,
        IEnumerable<string> playerAccountNames,
        CancellationToken ct = default) =>
        await FindByContentAsync(
            ctx,
            FightLogContentFingerprint.Create(fightType, fightStart, playerAccountNames),
            ct);

    public static async Task<FightLog?> FindByContentAsync(
        DatabaseContext ctx,
        FightLogContentFingerprint fingerprint,
        CancellationToken ct = default)
    {
        var window = TimeSpan.FromSeconds(2);
        var candidates = await ctx.FightLog
            .Where(f =>
                f.FightType == fingerprint.FightType &&
                f.FightStart >= fingerprint.FightStart - window &&
                f.FightStart <= fingerprint.FightStart + window)
            .OrderBy(f => f.FightStart)
            .ThenBy(f => f.FightLogId)
            .ToListAsync(ct);

        if (candidates.Count == 0)
        {
            return null;
        }

        var candidateIds = candidates.Select(f => f.FightLogId).ToList();
        var existingNamesByFightLogId = (await ctx.PlayerFightLog
            .Where(p => candidateIds.Contains(p.FightLogId))
            .Select(p => new { p.FightLogId, p.GuildWarsAccountName })
            .ToListAsync(ct))
            .ToLookup(p => p.FightLogId);

        FightLog? bestMatch = null;
        var bestMatchIsExact = false;
        var bestMatchPlayerCount = 0;
        var bestMatchStartDeltaTicks = long.MaxValue;

        foreach (var candidate in candidates)
        {
            var existingNames = existingNamesByFightLogId[candidate.FightLogId]
                .Select(p => p.GuildWarsAccountName)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            if (existingNames.Count == 0 || existingNames.All(fingerprint.PlayerAccountNames.Contains) is false)
            {
                continue;
            }

            var isExact = existingNames.Count == fingerprint.PlayerAccountNames.Count;
            var startDeltaTicks = Math.Abs((candidate.FightStart - fingerprint.FightStart).Ticks);
            if (bestMatch == null ||
                IsBetterMatch(
                    isExact,
                    existingNames.Count,
                    startDeltaTicks,
                    candidate.FightLogId,
                    bestMatchIsExact,
                    bestMatchPlayerCount,
                    bestMatchStartDeltaTicks,
                    bestMatch.FightLogId))
            {
                bestMatch = candidate;
                bestMatchIsExact = isExact;
                bestMatchPlayerCount = existingNames.Count;
                bestMatchStartDeltaTicks = startDeltaTicks;
            }
        }

        return bestMatch;
    }

    private static bool IsBetterMatch(
        bool isExact,
        int playerCount,
        long startDeltaTicks,
        long fightLogId,
        bool bestIsExact,
        int bestPlayerCount,
        long bestStartDeltaTicks,
        long bestFightLogId)
    {
        if (isExact != bestIsExact)
        {
            return isExact;
        }

        if (playerCount != bestPlayerCount)
        {
            return playerCount > bestPlayerCount;
        }

        if (startDeltaTicks != bestStartDeltaTicks)
        {
            return startDeltaTicks < bestStartDeltaTicks;
        }

        return fightLogId < bestFightLogId;
    }
}
