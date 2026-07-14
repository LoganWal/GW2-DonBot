using System.Security.Claims;
using DonBot.Core.Models.Entities;
using DonBot.Core.Models.Enums;
using DonBot.Core.Services;
using DonBot.Core.Services.GuildWars2;
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
        group.MapGet("/guilds", GetMyGuilds);
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

        var eligible = new List<(long FightLogId, string Url, string WingmanUrl)>();
        foreach (var log in logs)
        {
            if (ReportUrlHelper.TryParseReportUrl(log.Url, out var parsed, requireHttps: true))
            {
                eligible.Add((log.FightLogId, log.Url, parsed.CanonicalUrl));
            }
        }

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
                var wingmanUrl = $"https://gw2wingman.nevermindcreations.de/api/importLogQueued?link={Uri.EscapeDataString(log.WingmanUrl)}";
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
        string? guildIds = null,
        string? fightTypes = null,
        string? characters = null,
        string? playstyles = null,
        string? startDate = null,
        string? startDateTime = null,
        string? endDateTime = null,
        bool? isSuccess = null,
        int? fightMode = null,
        int? minDurationSeconds = null,
        int? maxDurationSeconds = null,
        decimal? minFightPercent = null,
        decimal? maxFightPercent = null,
        string sortField = "fightStart",
        string sortOrder = "desc",
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
        var selectedCharacterNames = new List<string>();

        if (!string.IsNullOrEmpty(characters))
        {
            selectedCharacterNames = characters.Split(',').Select(c => c.Trim()).Where(c => c.Length > 0).ToList();
            if (selectedCharacterNames.Count > 0)
            {
                playerLogQuery = playerLogQuery.Where(pfl => selectedCharacterNames.Contains(pfl.CharacterName));
            }
        }

        var selectedPlaystyles = (playstyles ?? string.Empty).Split(',')
            .Select(s => s.Trim())
            .Where(PlayerFightLogPlaystyleClassifier.IsKnownPlaystyle)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var needsPlaystyleQuery = selectedPlaystyles.Count > 0 || sortField == "playstyleLabel";
        var requiresFullPlayerHistory = needsPlaystyleQuery && await playerLogQuery
            .AnyAsync(p => p.Playstyle == string.Empty);
        if (selectedPlaystyles.Count > 0 && !requiresFullPlayerHistory)
        {
            playerLogQuery = playerLogQuery.Where(p => selectedPlaystyles.Contains(p.Playstyle));
        }

        var userPlayerLogs = requiresFullPlayerHistory
            ? await SelectPlaystyleFields(playerLogQuery).ToListAsync()
            : [];

        var initialFightLogIds = userPlayerLogs.Select(x => x.FightLogId).Distinct().ToList();
        var initialFightMeta = requiresFullPlayerHistory
            ? await context.FightLog
                .Where(fl => initialFightLogIds.Contains(fl.FightLogId))
                .Select(fl => new { fl.FightLogId, fl.FightType, fl.FightDurationInMs })
                .ToListAsync()
            : [];
        var initialTypeById = initialFightMeta.ToDictionary(fl => fl.FightLogId, fl => fl.FightType);
        var initialDurationById = initialFightMeta.ToDictionary(fl => fl.FightLogId, fl => fl.FightDurationInMs);
        var userWvwBenchmarks = PlayerFightLogPlaystyleClassifier.BuildWvwBenchmarks(
            userPlayerLogs.Where(p => initialTypeById.GetValueOrDefault(p.FightLogId) == (short)FightTypesEnum.WvW),
            initialDurationById);

        if (selectedPlaystyles.Count > 0)
        {
            userPlayerLogs = userPlayerLogs
                .Where(p => selectedPlaystyles.Contains(ResolvePlaystyle(p, initialTypeById, initialDurationById, userWvwBenchmarks)))
                .ToList();
        }

        var participatedLogIds = userPlayerLogs.Select(x => x.FightLogId).Distinct().ToList();
        var characterByFightLogId = userPlayerLogs
            .GroupBy(x => x.FightLogId)
            .ToDictionary(g => g.Key, g => g.OrderBy(x => x.CharacterName).First().CharacterName);
        var playstyleByFightLogId = userPlayerLogs
            .GroupBy(x => x.FightLogId)
            .ToDictionary(g => g.Key, g =>
            {
                var log = g.OrderBy(x => x.CharacterName).First();
                return ResolvePlaystyle(log, initialTypeById, initialDurationById, userWvwBenchmarks);
            });

        var query = requiresFullPlayerHistory
            ? context.FightLog.Where(fl => participatedLogIds.Contains(fl.FightLogId))
            : context.FightLog.Where(fl => playerLogQuery.Any(p => p.FightLogId == fl.FightLogId));

        if (guildId.HasValue)
        {
            query = query.Where(fl => fl.GuildId == guildId.Value);
        }

        if (!string.IsNullOrWhiteSpace(guildIds))
        {
            var selectedGuildIds = guildIds.Split(',')
                .Select(value => long.TryParse(value.Trim(), out var parsed) ? (long?)parsed : null)
                .Where(value => value.HasValue)
                .Select(value => value!.Value)
                .ToList();
            if (selectedGuildIds.Count > 0)
            {
                query = query.Where(fl => selectedGuildIds.Contains(fl.GuildId));
            }
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

        if (minDurationSeconds.HasValue)
        {
            query = query.Where(fl => fl.FightDurationInMs >= minDurationSeconds.Value * 1000L);
        }

        if (maxDurationSeconds.HasValue)
        {
            query = query.Where(fl => fl.FightDurationInMs <= maxDurationSeconds.Value * 1000L);
        }

        if (minFightPercent.HasValue)
        {
            query = query.Where(fl => fl.FightPercent >= minFightPercent.Value);
        }

        if (maxFightPercent.HasValue)
        {
            query = query.Where(fl => fl.FightPercent <= maxFightPercent.Value);
        }

        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 500);
        var total = await query.CountAsync();
        var offsetLong = ((long)page - 1) * pageSize;
        var offset = offsetLong < total ? (int)offsetLong : total;
        var descending = !string.Equals(sortOrder, "asc", StringComparison.OrdinalIgnoreCase);
        List<FightLog> logs;
        if (sortField == "fightType" ||
            (requiresFullPlayerHistory && (sortField == "playstyleLabel" || sortField == "characterName")))
        {
            var matchingLogs = await query
                .Select(fl => new { fl.FightLogId, fl.FightType })
                .ToListAsync();
            string SortValue(long id, short fightType) => sortField switch
            {
                "fightType" => FightDisplayName(fightType),
                "characterName" => characterByFightLogId.GetValueOrDefault(id, string.Empty),
                _ => PlayerFightLogPlaystyleClassifier.GetLabel(playstyleByFightLogId.GetValueOrDefault(id, string.Empty)),
            };
            var pageIds = (descending
                    ? matchingLogs.OrderByDescending(log => SortValue(log.FightLogId, log.FightType)).ThenByDescending(log => log.FightLogId)
                    : matchingLogs.OrderBy(log => SortValue(log.FightLogId, log.FightType)).ThenBy(log => log.FightLogId))
                .Skip(offset)
                .Take(pageSize)
                .Select(log => log.FightLogId)
                .ToList();
            var pageOrder = pageIds.Select((id, index) => (id, index)).ToDictionary(item => item.id, item => item.index);
            logs = (await context.FightLog.Where(fl => pageIds.Contains(fl.FightLogId)).ToListAsync())
                .OrderBy(fl => pageOrder[fl.FightLogId])
                .ToList();
        }
        else
        {
            IOrderedQueryable<FightLog> orderedQuery = sortField switch
            {
                "guildName" => descending
                    ? query.OrderByDescending(fl => fl.GuildId == -1 ? "Global" : context.Guild.Where(g => g.GuildId == fl.GuildId).Select(g => g.GuildName).FirstOrDefault() ?? fl.GuildId.ToString())
                    : query.OrderBy(fl => fl.GuildId == -1 ? "Global" : context.Guild.Where(g => g.GuildId == fl.GuildId).Select(g => g.GuildName).FirstOrDefault() ?? fl.GuildId.ToString()),
                "characterName" => descending
                    ? query.OrderByDescending(fl => context.PlayerFightLog
                        .Where(p => p.FightLogId == fl.FightLogId && gw2Names.Contains(p.GuildWarsAccountName) &&
                            (selectedCharacterNames.Count == 0 || selectedCharacterNames.Contains(p.CharacterName)) &&
                            (selectedPlaystyles.Count == 0 || selectedPlaystyles.Contains(p.Playstyle)))
                        .OrderBy(p => p.CharacterName).Select(p => p.CharacterName).FirstOrDefault())
                    : query.OrderBy(fl => context.PlayerFightLog
                        .Where(p => p.FightLogId == fl.FightLogId && gw2Names.Contains(p.GuildWarsAccountName) &&
                            (selectedCharacterNames.Count == 0 || selectedCharacterNames.Contains(p.CharacterName)) &&
                            (selectedPlaystyles.Count == 0 || selectedPlaystyles.Contains(p.Playstyle)))
                        .OrderBy(p => p.CharacterName).Select(p => p.CharacterName).FirstOrDefault()),
                "playstyleLabel" => descending
                    ? query.OrderByDescending(fl => context.PlayerFightLog
                        .Where(p => p.FightLogId == fl.FightLogId && gw2Names.Contains(p.GuildWarsAccountName) &&
                            (selectedCharacterNames.Count == 0 || selectedCharacterNames.Contains(p.CharacterName)) &&
                            (selectedPlaystyles.Count == 0 || selectedPlaystyles.Contains(p.Playstyle)))
                        .OrderBy(p => p.CharacterName)
                        .Select(p => p.Playstyle == PlayerFightLogPlaystyleClassifier.BoonDpsPlaystyle ? "Boon DPS"
                            : p.Playstyle == PlayerFightLogPlaystyleClassifier.BoonHealerPlaystyle ? "Boon Healer"
                            : p.Playstyle == PlayerFightLogPlaystyleClassifier.DpsPlaystyle ? "DPS"
                            : p.Playstyle == PlayerFightLogPlaystyleClassifier.WvwHealSupportPlaystyle ? "Heal Support"
                            : p.Playstyle == PlayerFightLogPlaystyleClassifier.MechanicPlaystyle ? "Mechanic"
                            : p.Playstyle == PlayerFightLogPlaystyleClassifier.WvwSupportPlaystyle ? "Support"
                            : p.Playstyle == PlayerFightLogPlaystyleClassifier.WvwSupportDpsPlaystyle ? "Support DPS"
                            : p.Playstyle)
                        .FirstOrDefault())
                    : query.OrderBy(fl => context.PlayerFightLog
                        .Where(p => p.FightLogId == fl.FightLogId && gw2Names.Contains(p.GuildWarsAccountName) &&
                            (selectedCharacterNames.Count == 0 || selectedCharacterNames.Contains(p.CharacterName)) &&
                            (selectedPlaystyles.Count == 0 || selectedPlaystyles.Contains(p.Playstyle)))
                        .OrderBy(p => p.CharacterName)
                        .Select(p => p.Playstyle == PlayerFightLogPlaystyleClassifier.BoonDpsPlaystyle ? "Boon DPS"
                            : p.Playstyle == PlayerFightLogPlaystyleClassifier.BoonHealerPlaystyle ? "Boon Healer"
                            : p.Playstyle == PlayerFightLogPlaystyleClassifier.DpsPlaystyle ? "DPS"
                            : p.Playstyle == PlayerFightLogPlaystyleClassifier.WvwHealSupportPlaystyle ? "Heal Support"
                            : p.Playstyle == PlayerFightLogPlaystyleClassifier.MechanicPlaystyle ? "Mechanic"
                            : p.Playstyle == PlayerFightLogPlaystyleClassifier.WvwSupportPlaystyle ? "Support"
                            : p.Playstyle == PlayerFightLogPlaystyleClassifier.WvwSupportDpsPlaystyle ? "Support DPS"
                            : p.Playstyle)
                        .FirstOrDefault()),
                "fightDurationInMs" => descending ? query.OrderByDescending(fl => fl.FightDurationInMs) : query.OrderBy(fl => fl.FightDurationInMs),
                "isSuccess" => descending ? query.OrderByDescending(fl => fl.IsSuccess) : query.OrderBy(fl => fl.IsSuccess),
                "fightPercent" => descending ? query.OrderByDescending(fl => fl.FightPercent) : query.OrderBy(fl => fl.FightPercent),
                _ => descending ? query.OrderByDescending(fl => fl.FightStart) : query.OrderBy(fl => fl.FightStart),
            };
            orderedQuery = descending
                ? orderedQuery.ThenByDescending(fl => fl.FightLogId)
                : orderedQuery.ThenBy(fl => fl.FightLogId);
            logs = await orderedQuery
                .Skip(offset)
                .Take(pageSize)
                .ToListAsync();
        }

        var pageGuildIds = logs.Select(fl => fl.GuildId).Distinct().ToList();
        var guildNameById = pageGuildIds.Count > 0
            ? await context.Guild
                .Where(g => pageGuildIds.Contains(g.GuildId))
                .ToDictionaryAsync(g => g.GuildId, g => g.GuildName ?? g.GuildId.ToString())
            : [];
        guildNameById[-1] = "Global";

        if (!requiresFullPlayerHistory)
        {
            var pageIds = logs.Select(fl => fl.FightLogId).ToList();
            var pagePlayerLogs = await playerLogQuery
                .Where(p => pageIds.Contains(p.FightLogId))
                .ToListAsync();
            foreach (var log in logs)
            {
                initialTypeById[log.FightLogId] = log.FightType;
                initialDurationById[log.FightLogId] = log.FightDurationInMs;
            }
            var needsWvwBenchmarks = pagePlayerLogs.Any(p =>
                initialTypeById.GetValueOrDefault(p.FightLogId) == (short)FightTypesEnum.WvW &&
                string.IsNullOrWhiteSpace(p.Playstyle));
            if (needsWvwBenchmarks)
            {
                var wvwPlayerLogs = await SelectPlaystyleFields(playerLogQuery)
                    .Where(p => context.FightLog.Any(fl => fl.FightLogId == p.FightLogId && fl.FightType == (short)FightTypesEnum.WvW))
                    .ToListAsync();
                var wvwFightIds = wvwPlayerLogs.Select(p => p.FightLogId).Distinct().ToList();
                var wvwDurations = await context.FightLog
                    .Where(fl => wvwFightIds.Contains(fl.FightLogId))
                    .ToDictionaryAsync(fl => fl.FightLogId, fl => fl.FightDurationInMs);
                userWvwBenchmarks = PlayerFightLogPlaystyleClassifier.BuildWvwBenchmarks(wvwPlayerLogs, wvwDurations);
            }
            foreach (var group in pagePlayerLogs.GroupBy(p => p.FightLogId))
            {
                var playerLog = group.OrderBy(p => p.CharacterName).First();
                characterByFightLogId[group.Key] = playerLog.CharacterName;
                playstyleByFightLogId[group.Key] = ResolvePlaystyle(playerLog, initialTypeById, initialDurationById, userWvwBenchmarks);
            }
        }

        var data = logs.Select(fl => new
        {
            fl.FightLogId,
            fl.GuildId,
            guildName = guildNameById.GetValueOrDefault(fl.GuildId, fl.GuildId.ToString()),
            fl.Url,
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

    private static string FightDisplayName(short fightType) => (FightTypesEnum)fightType switch
    {
        FightTypesEnum.WvW => "WvW",
        FightTypesEnum.Vale => "Vale Guardian",
        FightTypesEnum.Gorseval => "Gorseval",
        FightTypesEnum.Sabetha => "Sabetha",
        FightTypesEnum.Spirit => "Spirit Woods",
        FightTypesEnum.Sloth => "Slothasor",
        FightTypesEnum.Trio => "Trio",
        FightTypesEnum.Matthias => "Matthias",
        FightTypesEnum.Escort => "Escort",
        FightTypesEnum.Kc => "Keep Construct",
        FightTypesEnum.Tc => "Twisted Castle",
        FightTypesEnum.Xera => "Xera",
        FightTypesEnum.Cairn => "Cairn",
        FightTypesEnum.Mo => "Mursaat Overseer",
        FightTypesEnum.Samarog => "Samarog",
        FightTypesEnum.Deimos => "Deimos",
        FightTypesEnum.Sh => "Soulless Horror",
        FightTypesEnum.River => "River of Souls",
        FightTypesEnum.Bk => "Broken King",
        FightTypesEnum.EoS => "Eater of Souls",
        FightTypesEnum.SoD => "Voice in the Void",
        FightTypesEnum.Dhuum => "Dhuum",
        FightTypesEnum.Ca => "Conjured Amalgamate",
        FightTypesEnum.Largos => "Twin Largos",
        FightTypesEnum.Qadim => "Qadim",
        FightTypesEnum.Adina => "Cardinal Adina",
        FightTypesEnum.Sabir => "Cardinal Sabir",
        FightTypesEnum.Peerless => "Qadim the Peerless",
        FightTypesEnum.Greer => "Greer",
        FightTypesEnum.Decima => "Decima",
        FightTypesEnum.Ura => "Ura",
        FightTypesEnum.Kela => "Kela",
        FightTypesEnum.Ah => "Aetherblade Hideout",
        FightTypesEnum.Xjj => "Xunlai Jade Junkyard",
        FightTypesEnum.Ko => "Kaineng Overlook",
        FightTypesEnum.Ht => "Harvest Temple",
        FightTypesEnum.Olc => "Old Lion's Court",
        FightTypesEnum.Co => "Cosmic Observatory",
        FightTypesEnum.ToF => "Temple of Febe",
        FightTypesEnum.Icebrood => "Icebrood Construct",
        FightTypesEnum.Fraenir => "Fraenir",
        FightTypesEnum.Kodan => "Voice of the Fallen",
        FightTypesEnum.Whisper => "Whisper of Jormag",
        FightTypesEnum.Boneskinner => "Boneskinner",
        FightTypesEnum.Mama => "MAMA",
        FightTypesEnum.Siax => "Siax",
        FightTypesEnum.Ensolyss => "Ensolyss",
        FightTypesEnum.Skorvald => "Skorvald",
        FightTypesEnum.Artsariiv => "Artsariiv",
        FightTypesEnum.Arkk => "Arkk",
        FightTypesEnum.AiEle => "Ai (Ele)",
        FightTypesEnum.AiDark => "Ai (Dark)",
        FightTypesEnum.AiBoth => "Ai (Both)",
        FightTypesEnum.Kanaxai => "Kanaxai",
        FightTypesEnum.Eparch => "Eparch",
        FightTypesEnum.Shadow => "Shadow of the Dragon",
        FightTypesEnum.Golem => "Golem",
        _ => "Unknown",
    };

    private static IQueryable<PlayerFightLog> SelectPlaystyleFields(IQueryable<PlayerFightLog> query) =>
        query.Select(p => new PlayerFightLog
        {
            PlayerFightLogId = p.PlayerFightLogId,
            FightLogId = p.FightLogId,
            GuildWarsAccountName = p.GuildWarsAccountName,
            CharacterName = p.CharacterName,
            Damage = p.Damage,
            Healing = p.Healing,
            Cleanses = p.Cleanses,
            Strips = p.Strips,
            StabGenOnGroup = p.StabGenOnGroup,
            StabGenOffGroup = p.StabGenOffGroup,
            BoonRole = p.BoonRole,
            Playstyle = p.Playstyle,
        });

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

    private static async Task<IResult> GetMyGuilds(
        ClaimsPrincipal user,
        IDbContextFactory<DatabaseContext> dbContextFactory)
    {
        var discordIdStr = user.FindFirst("discord_id")?.Value;
        if (!long.TryParse(discordIdStr, out var discordId))
        {
            return Results.Unauthorized();
        }

        await using var context = await dbContextFactory.CreateDbContextAsync();
        var accountNames = await context.GuildWarsAccount
            .Where(account => account.DiscordId == discordId)
            .Select(account => account.GuildWarsAccountName)
            .ToListAsync();
        var fightLogIds = context.PlayerFightLog
            .Where(log => accountNames.Contains(log.GuildWarsAccountName))
            .Select(log => log.FightLogId);
        var guildIds = await context.FightLog
            .Where(log => fightLogIds.Contains(log.FightLogId))
            .Select(log => log.GuildId)
            .Distinct()
            .ToListAsync();
        var guildNames = await context.Guild
            .Where(guild => guildIds.Contains(guild.GuildId))
            .ToDictionaryAsync(guild => guild.GuildId, guild => guild.GuildName);
        var guilds = guildIds
            .Select(guildId => new
            {
                guildId = guildId.ToString(),
                guildName = guildId == -1
                    ? "Global"
                    : guildNames.GetValueOrDefault(guildId) ?? guildId.ToString(),
            })
            .OrderBy(guild => guild.guildId == "-1" ? 0 : 1)
            .ThenBy(guild => guild.guildName)
            .ToList();

        return Results.Ok(guilds);
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
