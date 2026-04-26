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
        group.MapGet("/mechanics", GetMechanicsOverview);
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

        var characters = playerLogs
            .Where(p => !string.IsNullOrEmpty(p.CharacterName))
            .GroupBy(p => p.CharacterName)
            .Select(g => new
            {
                characterName = g.Key,
                wvwLogs = g.Count(p => typeById.TryGetValue(p.FightLogId, out var t) && t == (short)FightTypesEnum.WvW),
                pveLogs = g.Count(p => typeById.TryGetValue(p.FightLogId, out var t) && t != (short)FightTypesEnum.WvW),
            })
            .OrderByDescending(c => c.wvwLogs + c.pveLogs)
            .ToList();

        return Results.Ok(new { wvw = wvwStats, pve = pveStats, characters });
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
            totalDeaths = logs.Sum(p => p.Deaths)
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
        short fightType = 0,
        string? startDateTime = null,
        string? endDateTime = null,
        bool? isSuccess = null,
        int? fightMode = null)
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
        var fightMetaQuery = context.FightLog
            .Where(fl => fightLogIds.Contains(fl.FightLogId) && fl.FightType == fightType);

        if (!string.IsNullOrEmpty(startDateTime) &&
            DateTime.TryParse(startDateTime, null, System.Globalization.DateTimeStyles.RoundtripKind, out var startDt))
            fightMetaQuery = fightMetaQuery.Where(fl => fl.FightStart >= startDt);

        if (!string.IsNullOrEmpty(endDateTime) &&
            DateTime.TryParse(endDateTime, null, System.Globalization.DateTimeStyles.RoundtripKind, out var endDt))
            fightMetaQuery = fightMetaQuery.Where(fl => fl.FightStart <= endDt);

        if (isSuccess.HasValue)
            fightMetaQuery = fightMetaQuery.Where(fl => fl.IsSuccess == isSuccess.Value);

        if (fightMode.HasValue)
            fightMetaQuery = fightMetaQuery.Where(fl => fl.FightMode == fightMode.Value);

        var fightMeta = await fightMetaQuery
            .Select(fl => new { fl.FightLogId, fl.FightStart, fl.FightDurationInMs, fl.IsSuccess, fl.FightMode })
            .OrderBy(fl => fl.FightStart)
            .ToListAsync();

        var metaById = fightMeta.ToDictionary(f => f.FightLogId);
        var playerLogById = playerLogs
            .GroupBy(p => p.FightLogId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(p => p.Damage).First());

        var isWvW = fightType == (short)FightTypesEnum.WvW;

        Dictionary<long, Dictionary<string, long>> mechanicsLookup = [];
        if (!isWvW)
        {
            var matchedPlayerLogIds = playerLogById.Values.Select(p => p.PlayerFightLogId).ToList();
            var mechanicsList = await context.PlayerFightLogMechanic
                .Where(m => matchedPlayerLogIds.Contains(m.PlayerFightLogId) && m.MechanicCount > 0)
                .ToListAsync();
            mechanicsLookup = mechanicsList
                .GroupBy(m => m.PlayerFightLogId)
                .ToDictionary(
                    g => g.Key,
                    g => g.GroupBy(m => m.MechanicName)
                           .ToDictionary(mg => mg.Key, mg => mg.Sum(m => m.MechanicCount)));
        }

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
                        characterName = p.CharacterName,
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
                    var mechanics = mechanicsLookup.TryGetValue(p.PlayerFightLogId, out var mDict)
                        ? mDict
                        : new Dictionary<string, long>();
                    return (object)new
                    {
                        fightLogId = f.FightLogId,
                        date = f.FightStart,
                        durationMs = f.FightDurationInMs,
                        characterName = p.CharacterName,
                        isSuccess = f.IsSuccess,
                        fightMode = f.FightMode,
                        dps,
                        cleaveDps,
                        healing = p.Healing,
                        cleanses = p.Cleanses,
                        quickness = (double)p.QuicknessDuration,
                        alacrity = (double)p.AlacDuration,
                        deaths = p.Deaths,
                        timesDowned = p.TimesDowned,
                        mechanics,
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
    };

    private static async Task<IResult> GetMechanicsOverview(
    ClaimsPrincipal user,
    IDbContextFactory<DatabaseContext> dbContextFactory)
{
    var discordIdStr = user.FindFirst("discord_id")?.Value;
    if (!long.TryParse(discordIdStr, out var discordId))
        return Results.Unauthorized();

    await using var context = await dbContextFactory.CreateDbContextAsync();

    var gw2Names = await context.GuildWarsAccount
        .Where(a => a.DiscordId == discordId)
        .Select(a => a.GuildWarsAccountName)
        .ToListAsync();

    var playerLogs = await context.PlayerFightLog
        .Where(pfl => gw2Names.Contains(pfl.GuildWarsAccountName))
        .Select(pfl => new { pfl.PlayerFightLogId, pfl.FightLogId })
        .ToListAsync();

    var fightLogIds = playerLogs.Select(p => p.FightLogId).Distinct().ToList();

    var fightTypeById = await context.FightLog
        .Where(fl => fightLogIds.Contains(fl.FightLogId) && fl.FightType != 0)
        .Select(fl => new { fl.FightLogId, fl.FightType })
        .ToDictionaryAsync(fl => fl.FightLogId, fl => (int)fl.FightType);

    var pvePlayerLogIds = playerLogs
        .Where(p => fightTypeById.ContainsKey(p.FightLogId))
        .Select(p => p.PlayerFightLogId)
        .ToList();

    var playerLogIdToFightType = playerLogs
        .Where(p => fightTypeById.ContainsKey(p.FightLogId))
        .ToDictionary(p => p.PlayerFightLogId, p => fightTypeById[p.FightLogId]);

    var mechanics = await context.PlayerFightLogMechanic
        .Where(m => pvePlayerLogIds.Contains(m.PlayerFightLogId) && m.MechanicCount > 0)
        .ToListAsync();

    var perRunValues = mechanics
        .GroupBy(m => (m.PlayerFightLogId, m.MechanicName))
        .Select(g => new
        {
            fightType = playerLogIdToFightType[g.Key.PlayerFightLogId],
            mechanicName = g.Key.MechanicName,
            count = g.Sum(m => m.MechanicCount),
        })
        .ToList();

    var result = perRunValues
        .GroupBy(x => (x.fightType, x.mechanicName))
        .Select(g =>
        {
            var values = g.Select(x => x.count).OrderBy(v => v).ToList();
            var mid = values.Count / 2;
            var median = values.Count % 2 == 0
                ? (values[mid - 1] + values[mid]) / 2L
                : values[mid];
            return new
            {
                fightType = g.Key.fightType,
                mechanicName = g.Key.mechanicName,
                max = values.Max(),
                avg = Math.Round(values.Average(v => (double)v), 1),
                median,
            };
        })
        .GroupBy(x => x.fightType)
        .Select(g => new
        {
            fightType = g.Key,
            mechanics = g.OrderByDescending(m => m.max)
                .Select(m => new { m.mechanicName, m.max, m.avg, m.median })
                .ToList(),
        })
        .Where(g => g.mechanics.Count > 0)
        .OrderBy(g => g.fightType)
        .ToList();

    return Results.Ok(result);
    }
}
