using DonBot.Models.Entities;
using DonBot.Models.Enums;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace DonBot.Api.Endpoints;

public static class StatsEndpoints
{
    public static void MapStatsEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/stats").RequireAuthorization();
        group.MapGet("/me", GetMyStats);
        group.MapGet("/bests", GetMyBests);
        group.MapGet("/progression", GetMyProgression);
    }

    private static async Task<IResult> GetMyStats(
        ClaimsPrincipal user,
        IDbContextFactory<DatabaseContext> dbContextFactory)
    {
        var discordIdStr = user.FindFirst("discord_id")?.Value;
        if (!long.TryParse(discordIdStr, out var discordId))
        {
            return Results.Unauthorized();
        }

        await using var context = await dbContextFactory.CreateDbContextAsync();

        var gw2Names = await context.GuildWarsAccount
            .Where(a => a.DiscordId == discordId)
            .Select(a => a.GuildWarsAccountName)
            .ToListAsync();

        var playerLogs = await context.PlayerFightLog
            .Where(pfl => gw2Names.Contains(pfl.GuildWarsAccountName))
            .ToListAsync();

        if (playerLogs.Count == 0)
        {
            return Results.Ok(new { wvw = (object?)null, pve = (object?)null });
        }

        var fightLogIds = playerLogs.Select(p => p.FightLogId).Distinct().ToList();
        var fightMeta = await context.FightLog
            .Where(fl => fightLogIds.Contains(fl.FightLogId))
            .Select(fl => new { fl.FightLogId, fl.FightType, fl.FightDurationInMs })
            .ToListAsync();

        var typeById = fightMeta.ToDictionary(x => x.FightLogId, x => x.FightType);
        var durationById = fightMeta.ToDictionary(x => x.FightLogId, x => (long)x.FightDurationInMs);

        var wvwLogs = playerLogs
            .Where(p => typeById.TryGetValue(p.FightLogId, out var t) && t == (short)FightTypesEnum.WvW)
            .ToList();

        var pveLogs = playerLogs
            .Where(p => typeById.TryGetValue(p.FightLogId, out var t) && t != (short)FightTypesEnum.WvW)
            .ToList();

        var wvwStats = wvwLogs.Count == 0 ? null : BuildWvwStats(wvwLogs, durationById);
        var pveStats = pveLogs.Count == 0 ? null : BuildPveStats(pveLogs, durationById);

        return Results.Ok(new { wvw = wvwStats, pve = pveStats });
    }

    private static object BuildWvwStats(List<PlayerFightLog> logs, Dictionary<long, long> durationById)
    {
        var totalDamage = logs.Sum(p => p.Damage);
        var totalCleave = logs.Sum(p => p.Cleave);
        var totalHealing = logs.Sum(p => p.Healing);
        var totalCleanses = logs.Sum(p => p.Cleanses);
        var totalStrips = logs.Sum(p => p.Strips);
        var totalDamageTaken = (double)logs.Sum(p => p.DamageTaken);
        var totalBarrierMitigation = (double)logs.Sum(p => p.BarrierMitigation);
        var totalDurationMs = logs.Sum(p => durationById.TryGetValue(p.FightLogId, out var d) ? d : 0);
        var s = totalDurationMs / 1000.0;

        return new
        {
            totalFights = logs.Count,
            totalFightDurationMs = totalDurationMs,
            // Damage
            totalDamage,
            avgDps = s > 0 ? (long)(totalDamage / s) : 0,
            totalCleave,
            avgCleaveDps = s > 0 ? (long)(totalCleave / s) : 0,
            totalDamageDownContribution = logs.Sum(p => p.DamageDownContribution),
            totalKills = logs.Sum(p => p.Kills),
            totalDowns = logs.Sum(p => p.Downs),
            totalDeaths = logs.Sum(p => p.Deaths),
            // Support
            totalHealing,
            avgHealingPerSecond = s > 0 ? (long)(totalHealing / s) : 0,
            totalBarrierGenerated = logs.Sum(p => p.BarrierGenerated),
            totalCleanses,
            avgCleansesPerSecond = s > 0 ? Math.Round(totalCleanses / s, 2) : 0,
            totalStrips,
            avgStripsPerSecond = s > 0 ? Math.Round(totalStrips / s, 2) : 0,
            // Boon gen
            avgQuickness = logs.Average(p => (double)p.QuicknessDuration),
            avgAlac = logs.Average(p => (double)p.AlacDuration),
            avgStabOnGroup = logs.Average(p => (double)p.StabGenOnGroup),
            avgStabOffGroup = logs.Average(p => (double)p.StabGenOffGroup),
            // Survivability / positioning
            totalTimesDowned = logs.Sum(p => p.TimesDowned),
            avgDistanceFromTag = logs.Average(p => (double)p.DistanceFromTag),
            // Aggregations (Attacks Missed)
            totalHitsWhileBlinded = logs.Sum(p => p.NumberOfHitsWhileBlinded),
            totalMissesAgainst = logs.Sum(p => p.NumberOfMissesAgainst),
            // Aggregations (Attacks Blocked)
            totalBlockedAttacks = logs.Sum(p => p.NumberOfTimesBlockedAttack),
            totalEnemyBlockedAttacks = logs.Sum(p => p.NumberOfTimesEnemyBlockedAttack),
            // Aggregations (Boons)
            totalBoonsRipped = logs.Sum(p => p.NumberOfBoonsRipped),
            // Damage taken
            totalDamageTaken,
            totalBarrierMitigation,
            barrierMitigationPercent = totalDamageTaken > 0
                ? Math.Round(totalBarrierMitigation / totalDamageTaken * 100, 2)
                : 0
        };
    }

    private static object BuildPveStats(List<PlayerFightLog> logs, Dictionary<long, long> durationById)
    {
        var totalDamage = logs.Sum(p => p.Damage);
        var totalCleave = logs.Sum(p => p.Cleave);
        var totalHealing = logs.Sum(p => p.Healing);
        var totalDurationMs = logs.Sum(p => durationById.TryGetValue(p.FightLogId, out var d) ? d : 0);
        var s = totalDurationMs / 1000.0;

        return new
        {
            totalFights = logs.Count,
            totalFightDurationMs = totalDurationMs,
            // Player overview (DPS-style)
            totalDamage,
            avgDps = s > 0 ? (long)(totalDamage / s) : 0,
            totalCleave,
            avgCleaveDps = s > 0 ? (long)(totalCleave / s) : 0,
            totalHealing,
            avgHealingPerSecond = s > 0 ? (long)(totalHealing / s) : 0,
            avgQuickness = logs.Average(p => (double)p.QuicknessDuration),
            avgAlac = logs.Average(p => (double)p.AlacDuration),
            // Survivability
            totalResurrectionTime = logs.Sum(p => p.ResurrectionTime),
            totalDamageTaken = logs.Sum(p => p.DamageTaken),
            totalTimesDowned = logs.Sum(p => p.TimesDowned),
            totalDeaths = logs.Sum(p => p.Deaths),
            // Cerus (Temple of Febe)
            totalCerusPhaseOneDamage = logs.Sum(p => (double)p.CerusPhaseOneDamage),
            totalCerusOrbsCollected = logs.Sum(p => p.CerusOrbsCollected),
            totalCerusSpreadHitCount = logs.Sum(p => p.CerusSpreadHitCount),
            // Deimos
            totalDeimosOilsTriggered = logs.Sum(p => p.DeimosOilsTriggered),
            // Ura
            totalShardPickUp = logs.Sum(p => p.ShardPickUp),
            totalShardUsed = logs.Sum(p => p.ShardUsed)
        };
    }

    private static async Task<IResult> GetMyBests(
        ClaimsPrincipal user,
        IDbContextFactory<DatabaseContext> dbContextFactory)
    {
        var discordIdStr = user.FindFirst("discord_id")?.Value;
        if (!long.TryParse(discordIdStr, out var discordId))
        {
            return Results.Unauthorized();
        }

        await using var context = await dbContextFactory.CreateDbContextAsync();

        var gw2Names = await context.GuildWarsAccount
            .Where(a => a.DiscordId == discordId)
            .Select(a => a.GuildWarsAccountName)
            .ToListAsync();

        var playerLogs = await context.PlayerFightLog
            .Where(pfl => gw2Names.Contains(pfl.GuildWarsAccountName))
            .ToListAsync();

        if (playerLogs.Count == 0)
        {
            return Results.Ok(new { wvw = (object?)null, pve = (object?)null });
        }

        var fightLogIds = playerLogs.Select(p => p.FightLogId).Distinct().ToList();
        var fightMeta = await context.FightLog
            .Where(fl => fightLogIds.Contains(fl.FightLogId))
            .Select(fl => new { fl.FightLogId, fl.FightType, fl.FightStart, fl.FightDurationInMs, fl.IsSuccess })
            .ToListAsync();

        var metaById = fightMeta.ToDictionary(
            x => x.FightLogId,
            x => (x.FightType, x.FightStart, x.FightDurationInMs));

        var wvwLogs = playerLogs
            .Where(p => metaById.TryGetValue(p.FightLogId, out var m) && m.FightType == (short)FightTypesEnum.WvW)
            .ToList();

        var pveLogs = playerLogs
            .Where(p => metaById.TryGetValue(p.FightLogId, out var m) && m.FightType != (short)FightTypesEnum.WvW)
            .ToList();

        // Best kill time per PvE boss type (successful kills only, exclude unknown)
        var pveFightLogIds = pveLogs.Select(p => p.FightLogId).ToHashSet();
        var playerLogByFightId = pveLogs.ToDictionary(p => p.FightLogId);
        var bestTimes = fightMeta
            .Where(f => f.IsSuccess
                && pveFightLogIds.Contains(f.FightLogId)
                && f.FightType != (short)FightTypesEnum.WvW
                && f.FightType != (short)FightTypesEnum.Unkn)
            .GroupBy(f => f.FightType)
            .Select(g =>
            {
                var best = g.MinBy(f => f.FightDurationInMs)!;
                var durationSeconds = best.FightDurationInMs / 1000.0;
                var playerDamage = playerLogByFightId.TryGetValue(best.FightLogId, out var pl) ? pl.Damage : 0;
                var dps = durationSeconds > 0 ? (long)(playerDamage / durationSeconds) : 0;
                return new
                {
                    fightType = (int)best.FightType,
                    durationMs = best.FightDurationInMs,
                    fightLogId = best.FightLogId,
                    fightDate = best.FightStart,
                    playerDps = dps
                };
            })
            .OrderBy(x => x.fightType)
            .ToList();

        return Results.Ok(new
        {
            wvw = wvwLogs.Count == 0 ? null : BuildWvwBests(wvwLogs, metaById),
            pve = pveLogs.Count == 0 ? null : BuildPveBests(pveLogs, metaById),
            bestTimes = bestTimes.Count == 0 ? null : bestTimes
        });
    }

    private static object Best<T>(
        List<PlayerFightLog> logs,
        Func<PlayerFightLog, T> selector,
        Dictionary<long, (short FightType, DateTime FightStart, long FightDurationInMs)> meta) where T : IComparable<T>
    {
        var best = logs.MaxBy(selector)!;
        var m = meta[best.FightLogId];
        return new
        {
            value = selector(best),
            durationMs = m.FightDurationInMs,
            fightLogId = best.FightLogId,
            fightType = (int)m.FightType,
            fightDate = m.FightStart
        };
    }

    private static object BestPerSecond(
        List<PlayerFightLog> logs,
        Func<PlayerFightLog, long> selector,
        Dictionary<long, (short FightType, DateTime FightStart, long FightDurationInMs)> meta)
    {
        var best = logs
            .Where(p => meta.TryGetValue(p.FightLogId, out var m) && m.FightDurationInMs > 0)
            .MaxBy(p => selector(p) / (meta[p.FightLogId].FightDurationInMs / 1000.0))!;
        var bm = meta[best.FightLogId];
        var durationSeconds = bm.FightDurationInMs / 1000.0;
        return new
        {
            value = durationSeconds > 0 ? (long)(selector(best) / durationSeconds) : 0,
            durationMs = bm.FightDurationInMs,
            fightLogId = best.FightLogId,
            fightType = (int)bm.FightType,
            fightDate = bm.FightStart
        };
    }

    private static object BuildWvwBests(
        List<PlayerFightLog> logs,
        Dictionary<long, (short FightType, DateTime FightStart, long FightDurationInMs)> meta) => new
    {
        damage = Best(logs, p => p.Damage, meta),
        damagePerSecond = BestPerSecond(logs, p => p.Damage, meta),
        kills = Best(logs, p => p.Kills, meta),
        killsPerSecond = BestPerSecond(logs, p => p.Kills, meta),
        downs = Best(logs, p => p.Downs, meta),
        downsPerSecond = BestPerSecond(logs, p => p.Downs, meta),
        downContribution = Best(logs, p => p.DamageDownContribution, meta),
        downContributionPerSecond = BestPerSecond(logs, p => p.DamageDownContribution, meta),
        cleanses = Best(logs, p => p.Cleanses, meta),
        cleansesPerSecond = BestPerSecond(logs, p => p.Cleanses, meta),
        strips = Best(logs, p => p.Strips, meta),
        stripsPerSecond = BestPerSecond(logs, p => p.Strips, meta),
        healing = Best(logs, p => p.Healing, meta),
        healingPerSecond = BestPerSecond(logs, p => p.Healing, meta),
        barrierGenerated = Best(logs, p => p.BarrierGenerated, meta),
        stabOnGroup = Best(logs, p => p.StabGenOnGroup, meta),
        stabOffGroup = Best(logs, p => p.StabGenOffGroup, meta),
        quickness = Best(logs, p => p.QuicknessDuration, meta),
        alacrity = Best(logs, p => p.AlacDuration, meta),
    };

    private static async Task<IResult> GetMyProgression(
        ClaimsPrincipal user,
        IDbContextFactory<DatabaseContext> dbContextFactory,
        short fightType = 0)
    {
        var discordIdStr = user.FindFirst("discord_id")?.Value;
        if (!long.TryParse(discordIdStr, out var discordId))
        {
            return Results.Unauthorized();
        }

        await using var context = await dbContextFactory.CreateDbContextAsync();

        var gw2Names = await context.GuildWarsAccount
            .Where(a => a.DiscordId == discordId)
            .Select(a => a.GuildWarsAccountName)
            .ToListAsync();

        var playerLogs = await context.PlayerFightLog
            .Where(pfl => gw2Names.Contains(pfl.GuildWarsAccountName))
            .ToListAsync();

        if (playerLogs.Count == 0)
        {
            return Results.Ok(Array.Empty<object>());
        }

        var fightLogIds = playerLogs.Select(p => p.FightLogId).Distinct().ToList();
        var fightMeta = await context.FightLog
            .Where(fl => fightLogIds.Contains(fl.FightLogId) && fl.FightType == fightType)
            .Select(fl => new { fl.FightLogId, fl.FightStart, fl.FightDurationInMs, fl.IsSuccess })
            .OrderBy(fl => fl.FightStart)
            .ToListAsync();

        var metaById = fightMeta.ToDictionary(f => f.FightLogId);
        var playerLogById = playerLogs.ToDictionary(p => p.FightLogId);

        var isWvW = fightType == (short)FightTypesEnum.WvW;

        var points = fightMeta
            .Where(f => playerLogById.ContainsKey(f.FightLogId))
            .Select(f =>
            {
                var p = playerLogById[f.FightLogId];
                var durationSeconds = f.FightDurationInMs / 1000.0;
                var dps = durationSeconds > 0 ? (long)(p.Damage / durationSeconds) : 0;
                var cleaveDps = durationSeconds > 0 ? (long)(p.Cleave / durationSeconds) : 0;

                if (isWvW)
                {
                    return (object)new
                    {
                        fightLogId = f.FightLogId,
                        date = f.FightStart,
                        durationMs = f.FightDurationInMs,
                        dps,
                        kills = p.Kills,
                        downs = p.Downs,
                        downContribution = p.DamageDownContribution,
                        cleanses = p.Cleanses,
                        strips = p.Strips,
                        healing = p.Healing,
                        deaths = p.Deaths,
                        quickness = (double)p.QuicknessDuration,
                        alacrity = (double)p.AlacDuration,
                    };
                }
                else
                {
                    return (object)new
                    {
                        fightLogId = f.FightLogId,
                        date = f.FightStart,
                        durationMs = f.FightDurationInMs,
                        isSuccess = f.IsSuccess,
                        dps,
                        cleaveDps,
                        healing = p.Healing,
                        cleanses = p.Cleanses,
                        quickness = (double)p.QuicknessDuration,
                        alacrity = (double)p.AlacDuration,
                        deaths = p.Deaths,
                        timesDowned = p.TimesDowned,
                    };
                }
            })
            .ToList();

        return Results.Ok(points);
    }

    private static object BuildPveBests(
        List<PlayerFightLog> logs,
        Dictionary<long, (short FightType, DateTime FightStart, long FightDurationInMs)> meta) => new
    {
        damage = Best(logs, p => p.Damage, meta),
        damagePerSecond = BestPerSecond(logs, p => p.Damage, meta),
        cleave = Best(logs, p => p.Cleave, meta),
        cleavePerSecond = BestPerSecond(logs, p => p.Cleave, meta),
        healing = Best(logs, p => p.Healing, meta),
        healingPerSecond = BestPerSecond(logs, p => p.Healing, meta),
        cleanses = Best(logs, p => p.Cleanses, meta),
        quickness = Best(logs, p => p.QuicknessDuration, meta),
        alacrity = Best(logs, p => p.AlacDuration, meta),
        barrierGenerated = Best(logs, p => p.BarrierGenerated, meta),
        cerusPhaseOneDamage = Best(logs, p => p.CerusPhaseOneDamage, meta),
        cerusOrbsCollected = Best(logs, p => p.CerusOrbsCollected, meta),
        deimosOilsTriggered = Best(logs, p => p.DeimosOilsTriggered, meta),
        shardPickUp = Best(logs, p => p.ShardPickUp, meta),
    };
}
