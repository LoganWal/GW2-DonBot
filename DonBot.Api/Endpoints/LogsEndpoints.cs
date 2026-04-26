using DonBot.Models.Entities;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Text.RegularExpressions;

namespace DonBot.Api.Endpoints;

public static class LogsEndpoints
{
    public static void MapLogsEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/logs").RequireAuthorization();
        group.MapGet("/", GetLogs);
        group.MapGet("/characters", GetMyCharacters);
        group.MapPost("/aggregate", AggregateLogs);
        group.MapPost("/wingman", SubmitToWingman);
        group.MapGet("/{id:long}", GetLog);
    }

    private record LogIdsRequest(List<long> LogIds);

    private static async Task<IResult> SubmitToWingman(
        LogIdsRequest request,
        IDbContextFactory<DatabaseContext> dbContextFactory,
        IHttpClientFactory httpClientFactory)
    {
        if (request?.LogIds == null || request.LogIds.Count == 0)
        {
            return Results.BadRequest("No log IDs provided.");
        }

        await using var context = await dbContextFactory.CreateDbContextAsync();

        var logs = await context.FightLog
            .Where(fl => request.LogIds.Contains(fl.FightLogId) && fl.FightType != 0 && fl.Url != null && fl.Url != string.Empty)
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

    private record AggregateLogsRequest(List<long> LogIds);

    private static async Task<IResult> AggregateLogs(
        AggregateLogsRequest request,
        IDbContextFactory<DatabaseContext> dbContextFactory)
    {
        var logIds = request?.LogIds;
        if (logIds == null || logIds.Count == 0)
        {
            return Results.BadRequest("No log IDs provided.");
        }

        await using var context = await dbContextFactory.CreateDbContextAsync();

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

        var allPlayerLogIds = playerLogs.Select(p => p.PlayerFightLogId).ToList();
        var allMechanics = await context.PlayerFightLogMechanic
            .Where(m => allPlayerLogIds.Contains(m.PlayerFightLogId) && m.MechanicCount > 0)
            .ToListAsync();

        var fightTypeByLogId = fightLogs.ToDictionary(fl => fl.FightLogId, fl => fl.FightType);
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

        var firstToDieCounts = playerLogs
            .GroupBy(pfl => pfl.FightLogId)
            .Where(g => g.Any(pfl => pfl.TimeOfDeath.HasValue))
            .Select(g => g.Where(pfl => pfl.TimeOfDeath.HasValue)
                .OrderBy(pfl => pfl.TimeOfDeath)
                .First().GuildWarsAccountName)
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
                    fightCount = fights.Count,
                    damage = (long)Math.Round(fights.Average(f => (double)f.Damage), 0),
                    damageDownContribution = (long)Math.Round(fights.Average(f => (double)f.DamageDownContribution), 0),
                    kills = fights.Sum(f => f.Kills),
                    downs = fights.Sum(f => f.Downs),
                    deaths = fights.Sum(f => f.Deaths),
                    timesDowned = fights.Sum(f => f.TimesDowned),
                    firstToDie = firstToDieCounts.GetValueOrDefault(g.Key, 0),
                    cleanses = Math.Round(fights.Average(f => (double)f.Cleanses), 0),
                    strips = Math.Round(fights.Average(f => (double)f.Strips), 0),
                    healing = (long)Math.Round(fights.Average(f => (double)f.Healing), 0),
                    barrierGenerated = (long)Math.Round(fights.Average(f => (double)f.BarrierGenerated), 0),
                    quicknessDuration = Math.Round(fights.Average(f => (double)f.QuicknessDuration), 2),
                    alacDuration = Math.Round(fights.Average(f => (double)f.AlacDuration), 2),
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
                    fightCount = fights.Count,
                    dps = totalSeconds > 0 ? (long)Math.Round(playerTotalDmg / totalSeconds, 0) : 0L,
                    cleaveDps = totalSeconds > 0 ? (long)Math.Round(playerTotalCleave / totalSeconds, 0) : 0L,
                    hps = totalSeconds > 0 ? (long)Math.Round(playerTotalHealing / totalSeconds, 0) : 0L,
                    deaths = fights.Sum(f => f.Deaths),
                    timesDowned = fights.Sum(f => f.TimesDowned),
                    firstToDie = firstToDieCounts.GetValueOrDefault(g.Key, 0),
                    quicknessDuration = Math.Round(fights.Average(f => (double)f.QuicknessDuration), 2),
                    alacDuration = Math.Round(fights.Average(f => (double)f.AlacDuration), 2),
                    barrierGenerated = (long)Math.Round(fights.Average(f => (double)f.BarrierGenerated), 0),
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
                    damageTaken = pfl.DamageTaken,
                    quicknessDuration = Math.Round((double)pfl.QuicknessDuration, 2),
                    alacDuration = Math.Round((double)pfl.AlacDuration, 2),
                    damageDownContribution = pfl.DamageDownContribution,
                    numberOfBoonsRipped = pfl.NumberOfBoonsRipped,
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
        });
    }

    private static async Task<IResult> GetLogs(
        ClaimsPrincipal user,
        IDbContextFactory<DatabaseContext> dbContextFactory,
        long? guildId = null,
        string? fightTypes = null,
        string? characters = null,
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

        var userPlayerLogs = await playerLogQuery
            .Select(pfl => new { pfl.FightLogId, pfl.CharacterName })
            .ToListAsync();

        var participatedLogIds = userPlayerLogs.Select(x => x.FightLogId).Distinct().ToList();
        var characterByFightLogId = userPlayerLogs
            .GroupBy(x => x.FightLogId)
            .ToDictionary(g => g.Key, g => g.First().CharacterName);

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
            query = query.Where(fl => fl.IsSuccess == isSuccess.Value);

        if (fightMode.HasValue)
            query = query.Where(fl => fl.FightMode == fightMode.Value);

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

        return Results.Ok(new { log, players = playerLogs, mechanics = mechanicLogs });
    }
}
