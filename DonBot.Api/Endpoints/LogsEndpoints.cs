using System.Security.Claims;
using System.Text.RegularExpressions;
using DonBot.Core.Models.Entities;
using DonBot.Core.Models.Enums;
using DonBot.Core.Services;
using DonBot.Services.GuildWarsServices;
using Microsoft.EntityFrameworkCore;

namespace DonBot.Api.Endpoints;

public static class LogsEndpoints
{
    public static void MapLogsEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/logs").RequireAuthorization();
        group.MapGet("/", GetLogs);
        group.MapGet("/characters", GetMyCharacters);
        group.MapPost("/aggregate", AggregateLogs);
        group.MapPost("/know-my-enemy", KnowMyEnemy);
        group.MapPost("/wingman", SubmitToWingman);
        group.MapGet("/{id:long}", GetLog);
    }

    private static async Task<IResult> SubmitToWingman(
        LogIdsRequest request,
        IDbContextFactory<DatabaseContext> dbContextFactory,
        IHttpClientFactory httpClientFactory)
    {
        List<long> logIds = request.LogIds ?? [];
        if (logIds.Count == 0)
        {
            return Results.BadRequest("No log IDs provided.");
        }

        await using var context = await dbContextFactory.CreateDbContextAsync();

        var logs = await context.FightLog
            .Where(fl => logIds.Contains(fl.FightLogId) && fl.FightType != 0 && fl.Url != string.Empty)
            .Select(fl => new { fl.FightLogId, fl.Url })
            .ToListAsync();

        var dpsReportPattern = new Regex(@"https://(?:b\.dps|wvw|dps)\.report/\S+");
        var eligible = logs.Where(l => dpsReportPattern.IsMatch(l.Url)).ToList();

        if (eligible.Count == 0)
        {
            return Results.Ok(new { submitted = 0, message = "No eligible dps.report URLs found (WvW logs are excluded)." });
        }

        var client = httpClientFactory.CreateClient();
        var results = new List<object>();

        foreach (var log in eligible)
        {
            try
            {
                var wingmanUrl = $"https://gw2wingman.nevermindcreations.de/api/importLogQueued?link={Uri.EscapeDataString(log.Url)}";
                var response = await client.GetAsync(wingmanUrl);
                results.Add(new { log.FightLogId, log.Url, success = response.IsSuccessStatusCode });
            }
            catch
            {
                results.Add(new { log.FightLogId, log.Url, success = false });
            }
        }

        return Results.Ok(new { submitted = results.Count, results });
    }

    private static async Task<IResult> KnowMyEnemy(
        LogIdsRequest request,
        IDbContextFactory<DatabaseContext> dbContextFactory,
        IDataModelGenerationService dataModelGenerationService)
    {
        List<long> logIds = request.LogIds ?? [];
        if (logIds.Count == 0)
        {
            return Results.BadRequest("No log IDs provided.");
        }

        await using var context = await dbContextFactory.CreateDbContextAsync();

        var logs = await context.FightLog
            .Where(fl => logIds.Contains(fl.FightLogId)
                && fl.FightType == (short)FightTypesEnum.WvW
                && fl.Url != string.Empty)
            .Select(fl => new { fl.FightLogId, fl.Url })
            .ToListAsync();

        if (logs.Count == 0)
        {
            return Results.Ok(new { logsProcessed = 0, totalTargets = 0, classes = Array.Empty<object>() });
        }

        var throttle = new SemaphoreSlim(4);
        var fetched = await Task.WhenAll(logs.Select(async log =>
        {
            await throttle.WaitAsync();
            try
            {
                var data = await dataModelGenerationService.GenerateEliteInsightDataModelFromUrl(log.Url);
                return data.FightEliteInsightDataModel.Targets ?? [];
            }
            catch
            {
                return [];
            }
            finally
            {
                throttle.Release();
            }
        }));

        var allTargets = fetched
            .SelectMany(t => t)
            .Where(t => t.Name != null && t.Name != "Dummy PvP Agent")
            .ToList();

        var classes = allTargets
            .GroupBy(t => t.Name!.Split(' ').FirstOrDefault() ?? "Unknown")
            .Select(grp =>
            {
                var avgStrike = grp.Average(t => t.Details?.DmgDistributions?
                    .Sum(d => d.Distribution?.Where(dis => dis[0].Bool == false).Sum(dis => dis[2].Double) ?? 0.0) ?? 0.0);
                var avgCondi = grp.Average(t => t.Details?.DmgDistributions?
                    .Sum(d => d.Distribution?.Where(dis => dis[0].Bool == true).Sum(dis => dis[2].Double) ?? 0.0) ?? 0.0);
                return new
                {
                    className = grp.Key,
                    count = grp.Count(),
                    avgStrike = (long)Math.Round(avgStrike),
                    avgCondi = (long)Math.Round(avgCondi),
                    avgTotal = (long)Math.Round(avgStrike + avgCondi),
                };
            })
            .OrderByDescending(c => c.avgTotal)
            .ToList();

        return Results.Ok(new
        {
            logsProcessed = logs.Count,
            totalTargets = allTargets.Count,
            classes,
        });
    }

    private static async Task<IResult> AggregateLogs(
        LogIdsRequest request,
        IDbContextFactory<DatabaseContext> dbContextFactory)
    {
        List<long> logIds = request.LogIds ?? [];
        if (logIds.Count == 0)
        {
            return Results.BadRequest("No log IDs provided.");
        }

        await using var context = await dbContextFactory.CreateDbContextAsync();
        return await AggregateLogsCoreAsync(logIds, context);
    }

    internal static async Task<IResult> AggregateLogsCoreAsync(List<long> logIds, DatabaseContext context)
    {
        var fightLogs = await context.FightLog
            .Where(fl => logIds.Contains(fl.FightLogId))
            .ToListAsync();

        if (fightLogs.Count == 0)
        {
            return Results.NotFound();
        }

        var playerLogs = await context.PlayerFightLog
            .Where(pfl => logIds.Contains(pfl.FightLogId))
            .ToListAsync();

        var pointAwards = await context.PlayerPointAward
            .Where(a => logIds.Contains(a.FightLogId))
            .ToListAsync();

        var allPlayerLogIds = playerLogs.Select(p => p.PlayerFightLogId).ToList();
        var allMechanics = await context.PlayerFightLogMechanic
            .Where(m => allPlayerLogIds.Contains(m.PlayerFightLogId) && m.MechanicCount > 0)
            .ToListAsync();

        var fightTypeByLogId = fightLogs.ToDictionary(fl => fl.FightLogId, fl => fl.FightType);
        var durationByLogId = fightLogs.ToDictionary(fl => fl.FightLogId, fl => fl.FightDurationInMs);
        var wvwBenchmarks = PlayerFightLogPlaystyleClassifier.BuildWvwBenchmarks(
            playerLogs.Where(p => fightTypeByLogId.GetValueOrDefault(p.FightLogId) == (short)FightTypesEnum.WvW),
            durationByLogId);
        var playerLogIdToFightType = playerLogs
            .Where(p => fightTypeByLogId.ContainsKey(p.FightLogId))
            .ToDictionary(p => p.PlayerFightLogId, p => fightTypeByLogId[p.FightLogId]);

        var playerIdToAccount = playerLogs.ToDictionary(p => p.PlayerFightLogId, p => p.GuildWarsAccountName);

        var mechanicsStats = allMechanics
            .Where(m => playerLogIdToFightType.ContainsKey(m.PlayerFightLogId))
            .GroupBy(m => playerLogIdToFightType[m.PlayerFightLogId])
            .Select(g =>
            {
                var byMechanic = g.GroupBy(m => m.MechanicName).ToList();
                var orderedMechanicNames = byMechanic
                    .OrderByDescending(mg => mg.Sum(m => m.MechanicCount))
                    .Select(mg => mg.Key)
                    .ToList();
                var playerRows = g
                    .GroupBy(m => playerIdToAccount.GetValueOrDefault(m.PlayerFightLogId, "?"))
                    .Select(pg => new
                    {
                        accountName = pg.Key,
                        counts = pg.GroupBy(m => m.MechanicName)
                            .ToDictionary(mg => mg.Key, mg => mg.Sum(m => m.MechanicCount)),
                    })
                    .OrderBy(p => p.accountName)
                    .ToList();
                return new
                {
                    fightType = (int)g.Key,
                    mechanicNames = orderedMechanicNames,
                    players = playerRows,
                };
            })
            .Where(g => g.mechanicNames.Count > 0)
            .OrderBy(g => g.fightType)
            .ToList();

        var wvwCount = fightLogs.Count(fl => fl.FightType == 0);
        var isWvW = wvwCount >= fightLogs.Count / 2.0;

        var totalDurationMs = fightLogs.Sum(fl => fl.FightDurationInMs);
        var sessionDurationMs = fightLogs.Count > 0
            ? (long)(fightLogs.Max(fl => fl.FightStart.AddMilliseconds(fl.FightDurationInMs)) - fightLogs.Min(fl => fl.FightStart)).TotalMilliseconds
            : 0L;

        var grouped = playerLogs.GroupBy(pfl => pfl.GuildWarsAccountName).ToList();

        var firstToDieByFight = playerLogs
            .GroupBy(pfl => pfl.FightLogId)
            .Where(g => g.Any(pfl => pfl.TimeOfDeath.HasValue))
            .ToDictionary(
                g => g.Key,
                g => g.Where(pfl => pfl.TimeOfDeath.HasValue)
                    .OrderBy(pfl => pfl.TimeOfDeath)
                    .First().GuildWarsAccountName);

        var firstToDieCounts = firstToDieByFight.Values
            .GroupBy(name => name)
            .ToDictionary(g => g.Key, g => g.Count());

        List<object> players;

        if (isWvW)
        {
            players = grouped.Select(g =>
            {
                var fights = g.ToList();
                return (object)new
                {
                    accountName = g.Key,
                    subGroup = fights.GroupBy(f => f.SubGroup).OrderByDescending(sg => sg.Count()).First().Key,
                    fightCount = fights.Count,
                    damage = fights.Sum(f => f.Damage),
                    damageDownContribution = fights.Sum(f => f.DamageDownContribution),
                    kills = fights.Sum(f => f.Kills),
                    downs = fights.Sum(f => f.Downs),
                    deaths = fights.Sum(f => f.Deaths),
                    timesDowned = fights.Sum(f => f.TimesDowned),
                    firstToDie = firstToDieCounts.GetValueOrDefault(g.Key, 0),
                    cleanses = fights.Sum(f => f.Cleanses),
                    strips = fights.Sum(f => f.Strips),
                    healing = fights.Sum(f => f.Healing),
                    barrierGenerated = fights.Sum(f => f.BarrierGenerated),
                    quicknessDuration = Math.Round(fights.Average(f => (double)f.QuicknessDuration), 2),
                    alacDuration = Math.Round(fights.Average(f => (double)f.AlacDuration), 2),
                    quicknessGenGroup = Math.Round(fights.Average(f => (double)f.QuicknessGenGroup), 2),
                    alacGenGroup = Math.Round(fights.Average(f => (double)f.AlacGenGroup), 2),
                    boonRole = MostCommonBoonRole(fights),
                    playstyle = PlaystyleSummaryLabel(fights, fightTypeByLogId, durationByLogId, wvwBenchmarks),
                    playstyleBreakdown = PlaystyleBreakdown(fights, fightTypeByLogId, durationByLogId, wvwBenchmarks),
                    stabOnGroup = Math.Round(fights.Average(f => (double)f.StabGenOnGroup), 2),
                    stabOffGroup = Math.Round(fights.Average(f => (double)f.StabGenOffGroup), 2),
                    distanceFromTag = Math.Round(
                        fights.Any(f => f.DistanceFromTag < 1100)
                            ? fights.Where(f => f.DistanceFromTag < 1100).Average(f => (double)f.DistanceFromTag)
                            : 0, 2),
                    damageTaken = fights.Sum(f => f.DamageTaken),
                    barrierMitigation = fights.Sum(f => f.BarrierMitigation),
                    interrupts = fights.Sum(f => f.Interrupts),
                    timesInterrupted = fights.Sum(f => f.TimesInterrupted),
                    numberOfBoonsRipped = fights.Sum(f => f.NumberOfBoonsRipped),
                    resurrectionTime = fights.Sum(f => f.ResurrectionTime),
                };
            }).OrderByDescending(p => ((dynamic)p).damage).ToList();
        }
        else
        {
            var totalSeconds = totalDurationMs / 1000.0;
            players = grouped.Select(g =>
            {
                var fights = g.ToList();
                var playerTotalDmg = fights.Sum(f => f.Damage);
                var playerTotalCleave = fights.Sum(f => f.Cleave);
                var playerTotalHealing = fights.Sum(f => f.Healing);
                return (object)new
                {
                    accountName = g.Key,
                    subGroup = fights.GroupBy(f => f.SubGroup).OrderByDescending(sg => sg.Count()).First().Key,
                    fightCount = fights.Count,
                    dps = totalSeconds > 0 ? (long)Math.Round(playerTotalDmg / totalSeconds, 0) : 0L,
                    cleaveDps = totalSeconds > 0 ? (long)Math.Round(playerTotalCleave / totalSeconds, 0) : 0L,
                    healing = fights.Sum(f => f.Healing),
                    hps = totalSeconds > 0 ? (long)Math.Round(playerTotalHealing / totalSeconds, 0) : 0L,
                    cleanses = fights.Sum(f => f.Cleanses),
                    strips = fights.Sum(f => f.Strips),
                    stabOnGroup = Math.Round(fights.Average(f => (double)f.StabGenOnGroup), 2),
                    stabOffGroup = Math.Round(fights.Average(f => (double)f.StabGenOffGroup), 2),
                    deaths = fights.Sum(f => f.Deaths),
                    timesDowned = fights.Sum(f => f.TimesDowned),
                    firstToDie = firstToDieCounts.GetValueOrDefault(g.Key, 0),
                    quicknessDuration = Math.Round(fights.Average(f => (double)f.QuicknessDuration), 2),
                    alacDuration = Math.Round(fights.Average(f => (double)f.AlacDuration), 2),
                    quicknessGenGroup = Math.Round(fights.Average(f => (double)f.QuicknessGenGroup), 2),
                    alacGenGroup = Math.Round(fights.Average(f => (double)f.AlacGenGroup), 2),
                    boonRole = MostCommonBoonRole(fights),
                    playstyle = PlaystyleSummaryLabel(fights, fightTypeByLogId, durationByLogId, wvwBenchmarks),
                    playstyleBreakdown = PlaystyleBreakdown(fights, fightTypeByLogId, durationByLogId, wvwBenchmarks),
                    barrierGenerated = fights.Sum(f => f.BarrierGenerated),
                    damageTaken = fights.Sum(f => f.DamageTaken),
                    resurrectionTime = fights.Sum(f => f.ResurrectionTime),
                };
            }).OrderByDescending(p => ((dynamic)p).dps).ToList();
        }

        var playerLogsByFight = playerLogs.GroupBy(pfl => pfl.FightLogId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var orderedFightLogs = fightLogs.OrderBy(fl => fl.FightStart).ToList();

        var timeline = orderedFightLogs.Select(fl =>
        {
            var fightPlayers = playerLogsByFight.GetValueOrDefault(fl.FightLogId, []);
            var durationSec = fl.FightDurationInMs / 1000.0;
            var firstDeadAccount = firstToDieByFight.GetValueOrDefault(fl.FightLogId);
            return new
            {
                fightLogId = fl.FightLogId,
                fightType = fl.FightType,
                fightStart = fl.FightStart,
                fightDurationMs = fl.FightDurationInMs,
                players = fightPlayers.Select(pfl => new
                {
                    accountName = pfl.GuildWarsAccountName,
                    damage = pfl.Damage,
                    dps = durationSec > 0 ? (long)Math.Round(pfl.Damage / durationSec, 0) : 0L,
                    cleaveDps = durationSec > 0 ? (long)Math.Round(pfl.Cleave / durationSec, 0) : 0L,
                    deaths = pfl.Deaths,
                    timesDowned = pfl.TimesDowned,
                    kills = pfl.Kills,
                    downs = pfl.Downs,
                    healing = pfl.Healing,
                    cleanses = pfl.Cleanses,
                    strips = pfl.Strips,
                    damageTaken = pfl.DamageTaken,
                    quicknessDuration = Math.Round((double)pfl.QuicknessDuration, 2),
                    alacDuration = Math.Round((double)pfl.AlacDuration, 2),
                    quicknessGenGroup = Math.Round((double)pfl.QuicknessGenGroup, 2),
                    alacGenGroup = Math.Round((double)pfl.AlacGenGroup, 2),
                    boonRole = pfl.BoonRole,
                    playstyle = ResolvePlaystyle(pfl, fightTypeByLogId, durationByLogId, wvwBenchmarks),
                    damageDownContribution = pfl.DamageDownContribution,
                    numberOfBoonsRipped = pfl.NumberOfBoonsRipped,
                    barrierGenerated = pfl.BarrierGenerated,
                    barrierMitigation = pfl.BarrierMitigation,
                    stabOnGroup = Math.Round((double)pfl.StabGenOnGroup, 2),
                    stabOffGroup = Math.Round((double)pfl.StabGenOffGroup, 2),
                    interrupts = pfl.Interrupts,
                    timesInterrupted = pfl.TimesInterrupted,
                    resurrectionTime = pfl.ResurrectionTime,
                    firstToDie = pfl.GuildWarsAccountName == firstDeadAccount ? 1 : 0,
                    distanceFromTag = pfl.DistanceFromTag < 1100 ? Math.Round((double)pfl.DistanceFromTag, 2) : 0,
                }).ToList(),
            };
        }).ToList();

        var logs = orderedFightLogs
            .OrderByDescending(fl => fl.FightStart)
            .Select(fl => new
            {
                fl.FightLogId,
                fl.FightType,
                fl.FightMode,
                fl.FightStart,
                fl.FightDurationInMs,
                fl.IsSuccess,
                fl.FightPercent,
                fl.Url,
            })
            .ToList();

        return Results.Ok(new
        {
            type = isWvW ? "wvw" : "pve",
            totalLogs = fightLogs.Count,
            totalDurationMs,
            sessionDurationMs,
            logs,
            timeline,
            players,
            mechanics = mechanicsStats,
            points = BuildPointsBreakdown(playerLogs, pointAwards, fightLogs),
        });
    }

    private static string MostCommonBoonRole(IEnumerable<PlayerFightLog> fights) =>
        fights
            .Select(f => f.BoonRole)
            .Where(role => !string.IsNullOrWhiteSpace(role))
            .GroupBy(role => role)
            .OrderByDescending(g => g.Count())
            .ThenBy(g => g.Key)
            .Select(g => g.Key)
            .FirstOrDefault() ?? string.Empty;

    private static string ResolvePlaystyle(
        PlayerFightLog log,
        IReadOnlyDictionary<long, short> fightTypeByLogId,
        IReadOnlyDictionary<long, long> durationByLogId,
        WvwPlaystyleBenchmarks wvwBenchmarks)
    {
        var isWvW = fightTypeByLogId.GetValueOrDefault(log.FightLogId) == (short)FightTypesEnum.WvW;
        return PlayerFightLogPlaystyleClassifier.ResolvePlaystyle(
            log,
            isWvW,
            durationByLogId.GetValueOrDefault(log.FightLogId),
            isWvW ? wvwBenchmarks : null);
    }

    private static IReadOnlyList<PlaystyleBreakdownRow> PlaystyleBreakdown(
        IEnumerable<PlayerFightLog> fights,
        IReadOnlyDictionary<long, short> fightTypeByLogId,
        IReadOnlyDictionary<long, long> durationByLogId,
        WvwPlaystyleBenchmarks wvwBenchmarks) =>
        fights
            .Select(f => ResolvePlaystyle(f, fightTypeByLogId, durationByLogId, wvwBenchmarks))
            .GroupBy(style => style)
            .OrderByDescending(g => g.Count())
            .ThenBy(g => PlayerFightLogPlaystyleClassifier.GetLabel(g.Key))
            .Select(g => new PlaystyleBreakdownRow(
                g.Key,
                PlayerFightLogPlaystyleClassifier.GetLabel(g.Key),
                g.Count()))
            .ToList();

    private static string PlaystyleSummaryLabel(
        IEnumerable<PlayerFightLog> fights,
        IReadOnlyDictionary<long, short> fightTypeByLogId,
        IReadOnlyDictionary<long, long> durationByLogId,
        WvwPlaystyleBenchmarks wvwBenchmarks)
    {
        var breakdown = PlaystyleBreakdown(fights, fightTypeByLogId, durationByLogId, wvwBenchmarks);
        return breakdown.Count == 1
            ? breakdown[0].Label
            : "Mixed";
    }

    private sealed record PlaystyleBreakdownRow(string Key, string Label, int Count);

    private static async Task<IResult> GetLogs(
        ClaimsPrincipal user,
        IDbContextFactory<DatabaseContext> dbContextFactory,
        long? guildId = null,
        string? fightTypes = null,
        string? characters = null,
        string? playstyles = null,
        string? startDate = null,
        string? startDateTime = null,
        string? endDateTime = null,
        bool? isSuccess = null,
        int? fightMode = null,
        int page = 1,
        int pageSize = 20)
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

        var playerLogQuery = context.PlayerFightLog
            .Where(pfl => gw2Names.Contains(pfl.GuildWarsAccountName));

        if (!string.IsNullOrEmpty(characters))
        {
            var charList = characters.Split(',').Select(c => c.Trim()).Where(c => c.Length > 0).ToList();
            if (charList.Count > 0)
            {
                playerLogQuery = playerLogQuery.Where(pfl => charList.Contains(pfl.CharacterName));
            }
        }

        var userPlayerLogs = await playerLogQuery.ToListAsync();

        var initialFightLogIds = userPlayerLogs.Select(x => x.FightLogId).Distinct().ToList();
        var initialFightMeta = await context.FightLog
            .Where(fl => initialFightLogIds.Contains(fl.FightLogId))
            .Select(fl => new { fl.FightLogId, fl.FightType, fl.FightDurationInMs })
            .ToListAsync();
        var initialTypeById = initialFightMeta.ToDictionary(fl => fl.FightLogId, fl => fl.FightType);
        var initialDurationById = initialFightMeta.ToDictionary(fl => fl.FightLogId, fl => fl.FightDurationInMs);
        var userWvwBenchmarks = PlayerFightLogPlaystyleClassifier.BuildWvwBenchmarks(
            userPlayerLogs.Where(p => initialTypeById.GetValueOrDefault(p.FightLogId) == (short)FightTypesEnum.WvW),
            initialDurationById);

        if (!string.IsNullOrWhiteSpace(playstyles))
        {
            var selectedPlaystyles = playstyles.Split(',')
                .Select(s => s.Trim())
                .Where(PlayerFightLogPlaystyleClassifier.IsKnownPlaystyle)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            if (selectedPlaystyles.Count > 0)
            {
                userPlayerLogs = userPlayerLogs
                    .Where(p => selectedPlaystyles.Contains(ResolvePlaystyle(p, initialTypeById, initialDurationById, userWvwBenchmarks)))
                    .ToList();
            }
        }

        var participatedLogIds = userPlayerLogs.Select(x => x.FightLogId).Distinct().ToList();
        var characterByFightLogId = userPlayerLogs
            .GroupBy(x => x.FightLogId)
            .ToDictionary(g => g.Key, g => g.First().CharacterName);
        var playstyleByFightLogId = userPlayerLogs
            .GroupBy(x => x.FightLogId)
            .ToDictionary(g => g.Key, g =>
            {
                var log = g.First();
                return ResolvePlaystyle(log, initialTypeById, initialDurationById, userWvwBenchmarks);
            });

        var query = context.FightLog
            .Where(fl => participatedLogIds.Contains(fl.FightLogId));

        if (guildId.HasValue)
        {
            query = query.Where(fl => fl.GuildId == guildId.Value);
        }

        if (!string.IsNullOrEmpty(fightTypes))
        {
            var types = fightTypes.Split(',')
                .Select(s => short.TryParse(s.Trim(), out var v) ? (short?)v : null)
                .Where(v => v.HasValue)
                .Select(v => v!.Value)
                .ToList();

            if (types.Count > 0)
            {
                query = query.Where(fl => types.Contains(fl.FightType));
            }
        }

        if (!string.IsNullOrEmpty(startDateTime) &&
            DateTime.TryParse(startDateTime, null, System.Globalization.DateTimeStyles.RoundtripKind, out var startDt))
        {
            query = query.Where(fl => fl.FightStart >= startDt);
        }
        else if (!string.IsNullOrEmpty(startDate) && DateOnly.TryParse(startDate, out var date))
        {
            var start = date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
            var end = date.ToDateTime(TimeOnly.MaxValue, DateTimeKind.Utc);
            query = query.Where(fl => fl.FightStart >= start && fl.FightStart <= end);
        }

        if (!string.IsNullOrEmpty(endDateTime) &&
            DateTime.TryParse(endDateTime, null, System.Globalization.DateTimeStyles.RoundtripKind, out var endDt))
        {
            query = query.Where(fl => fl.FightStart <= endDt);
        }

        if (isSuccess.HasValue)
        {
            query = query.Where(fl => fl.IsSuccess == isSuccess.Value);
        }

        if (fightMode.HasValue)
        {
            query = query.Where(fl => fl.FightMode == fightMode.Value);
        }

        var total = await query.CountAsync();
        var logs = await query
            .OrderByDescending(fl => fl.FightStart)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var data = logs.Select(fl => new
        {
            fl.FightLogId,
            fl.FightType,
            fl.FightMode,
            fl.FightStart,
            fl.FightDurationInMs,
            fl.IsSuccess,
            fl.FightPercent,
            characterName = characterByFightLogId.TryGetValue(fl.FightLogId, out var cn) ? cn : string.Empty,
            playstyle = playstyleByFightLogId.TryGetValue(fl.FightLogId, out var ps) ? ps : string.Empty,
            playstyleLabel = playstyleByFightLogId.TryGetValue(fl.FightLogId, out var psl) ? PlayerFightLogPlaystyleClassifier.GetLabel(psl) : string.Empty,
        }).ToList();

        return Results.Ok(new { total, page, pageSize, data });
    }

    private static async Task<IResult> GetMyCharacters(
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

        var chars = await context.PlayerFightLog
            .Where(pfl => gw2Names.Contains(pfl.GuildWarsAccountName) && pfl.CharacterName != string.Empty)
            .Select(pfl => pfl.CharacterName)
            .Distinct()
            .OrderBy(c => c)
            .ToListAsync();

        return Results.Ok(chars);
    }

    private static async Task<IResult> GetLog(
        long id,
        IDbContextFactory<DatabaseContext> dbContextFactory)
    {
        await using var context = await dbContextFactory.CreateDbContextAsync();
        return await GetLogCoreAsync(id, context);
    }

    internal static async Task<IResult> GetLogCoreAsync(long id, DatabaseContext context)
    {
        var log = await context.FightLog.FirstOrDefaultAsync(fl => fl.FightLogId == id);
        if (log is null)
        {
            return Results.NotFound();
        }

        var playerLogs = await context.PlayerFightLog
            .Where(pfl => pfl.FightLogId == id)
            .ToListAsync();

        var playerLogIds = playerLogs.Select(p => p.PlayerFightLogId).ToList();
        var mechanicLogs = await context.PlayerFightLogMechanic
            .Where(m => playerLogIds.Contains(m.PlayerFightLogId))
            .ToListAsync();

        var pointAwards = await context.PlayerPointAward
            .Where(a => a.FightLogId == id)
            .ToListAsync();

        return Results.Ok(new { log, players = playerLogs, mechanics = mechanicLogs, points = BuildPointsBreakdown(playerLogs, pointAwards, [log]) });
    }

    private static object BuildPointsBreakdown(
        IReadOnlyList<PlayerFightLog> playerLogs,
        IReadOnlyList<PlayerPointAward> pointAwards,
        IReadOnlyList<FightLog> fightLogs)
    {
        var fightById = fightLogs.ToDictionary(f => f.FightLogId);
        var awardsByPlayerFightLogId = pointAwards
            .GroupBy(a => a.PlayerFightLogId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var playerRows = playerLogs
            .GroupBy(p => p.GuildWarsAccountName)
            .Select(g =>
            {
                var awards = g
                    .SelectMany(player => awardsByPlayerFightLogId.GetValueOrDefault(player.PlayerFightLogId, []))
                    .ToList();
                return new
                {
                    accountName = g.Key,
                    fightCount = g.Select(p => p.FightLogId).Distinct().Count(),
                    totalPoints = Math.Round(awards.Sum(a => a.Points), 3),
                    components = awards
                        .GroupBy(a => new { a.Metric, a.MetricLabel })
                        .Select(cg => new
                        {
                            metric = cg.Key.Metric,
                            metricLabel = cg.Key.MetricLabel,
                            points = Math.Round(cg.Sum(a => a.Points), 3),
                            count = cg.Count(),
                            bestValue = cg.Max(a => a.MetricValue),
                            percentileValue = cg.Max(a => a.PercentileValue),
                        })
                        .OrderByDescending(c => c.points)
                        .ThenBy(c => c.metricLabel)
                        .ToList(),
                    logs = awards
                        .GroupBy(a => a.FightLogId)
                        .Select(lg =>
                        {
                            fightById.TryGetValue(lg.Key, out var fight);
                            return new
                            {
                                fightLogId = lg.Key,
                                fightType = fight?.FightType ?? 0,
                                fightStart = fight?.FightStart,
                                totalPoints = Math.Round(lg.Sum(a => a.Points), 3),
                                components = lg
                                    .OrderByDescending(a => a.Points)
                                    .Select(a => new
                                    {
                                        a.Metric,
                                        a.MetricLabel,
                                        a.MetricValue,
                                        a.PercentileValue,
                                        a.BasePoints,
                                        a.Multiplier,
                                        a.Points,
                                        a.Reason,
                                    })
                                    .ToList(),
                            };
                        })
                        .OrderByDescending(l => l.totalPoints)
                        .ThenByDescending(l => l.fightStart)
                        .ToList()
                };
            })
            .OrderByDescending(p => p.totalPoints)
            .ThenBy(p => p.accountName)
            .ToList();

        var componentRows = pointAwards
            .GroupBy(a => new { a.Metric, a.MetricLabel })
            .Select(g => new
            {
                metric = g.Key.Metric,
                metricLabel = g.Key.MetricLabel,
                points = Math.Round(g.Sum(a => a.Points), 3),
                count = g.Count(),
            })
            .OrderByDescending(g => g.points)
            .ThenBy(g => g.metricLabel)
            .ToList();

        return new
        {
            totalPoints = Math.Round(pointAwards.Sum(a => a.Points), 3),
            awardedPlayers = playerRows.Count(p => p.totalPoints > 0),
            components = componentRows,
            players = playerRows,
        };
    }

    // ASP.NET Core model binding instantiates this request DTO.
    // ReSharper disable ClassNeverInstantiated.Local
    // ReSharper disable UnusedAutoPropertyAccessor.Local
    private sealed class LogIdsRequest
    {
        public List<long>? LogIds { get; init; }
    }
    // ReSharper restore UnusedAutoPropertyAccessor.Local
    // ReSharper restore ClassNeverInstantiated.Local
}
