using System.Security.Claims;
using System.Text.Json;
using DonBot.Api.Services;
using DonBot.Core.Services.RaidLifecycle;
using DonBot.Models.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace DonBot.Api.Endpoints;

public static class LiveRaidEndpoints
{
    public static void MapLiveRaidEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/live-raid").RequireAuthorization();

        group.MapGet("/guilds", ListGuilds);
        group.MapGet("/{guildId:long}", GetLatestRaid);
        group.MapGet("/{guildId:long}/aggregate", GetAggregate);
        group.MapGet("/{guildId:long}/logs/{fightLogId:long}", GetSingleLog);
        group.MapGet("/{guildId:long}/stream", StreamUpdates);
        group.MapPost("/{guildId:long}/start", StartRaid);
        group.MapPost("/{guildId:long}/stop", StopRaid);
    }

    private record GuildListItem(string guildId, string guildName);

    private static readonly TimeSpan GuildListCacheTtl = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan GuildListErrorTtl = TimeSpan.FromSeconds(5);

    private static async Task<IResult> ListGuilds(
        ClaimsPrincipal user,
        [FromServices] IUserGuildsService userGuilds,
        [FromServices] IDbContextFactory<DatabaseContext> dbContextFactory,
        [FromServices] IMemoryCache cache)
    {
        var discordId = user.FindFirst("discord_id")?.Value;
        if (string.IsNullOrEmpty(discordId))
        {
            return Results.Unauthorized();
        }

        var cacheKey = $"live-raid:guilds-response:{discordId}";
        var result = await cache.GetOrCoalesceAsync(cacheKey, GuildListCacheTtl, GuildListErrorTtl, async () =>
        {
            var userGuildList = await userGuilds.GetForPrincipalAsync(user);
            if (userGuildList is null)
            {
                return new List<GuildListItem>();
            }

            var userGuildIds = userGuildList.Select(g => (long)g.Id).ToHashSet();

            await using var ctx = await dbContextFactory.CreateDbContextAsync();
            return await ctx.Guild
                .Where(g => userGuildIds.Contains(g.GuildId)
                    && ctx.FightsReport.Any(r => r.GuildId == g.GuildId && r.GuildId > 0))
                .OrderBy(g => g.GuildName)
                .Select(g => new GuildListItem(g.GuildId.ToString(), g.GuildName ?? g.GuildId.ToString()))
                .ToListAsync();
        });

        return Results.Ok(result);
    }

    private static async Task<IResult> GetLatestRaid(
        long guildId,
        ClaimsPrincipal user,
        [FromServices] IUserGuildsService userGuilds,
        [FromServices] IRaidLifecycleService raidLifecycle,
        [FromServices] IDbContextFactory<DatabaseContext> dbContextFactory)
    {
        if (await EnsureAuthorizedAsync(user, guildId, userGuilds) is { } denied)
        {
            return denied;
        }

        var report = await raidLifecycle.GetLatestRaidAsync(guildId);
        if (report == null)
        {
            return Results.NotFound();
        }

        await using var ctx = await dbContextFactory.CreateDbContextAsync();
        var fightLogIds = await GetFightLogIdsInWindowAsync(ctx, report);

        return Results.Ok(new
        {
            reportId = report.FightsReportId,
            // Discord snowflakes exceed JS Number.MAX_SAFE_INTEGER; serialize as string so the
            // client doesn't truncate the last digits when parsing.
            guildId = report.GuildId.ToString(),
            fightsStart = report.FightsStart,
            fightsEnd = report.FightsEnd,
            isOpen = report.FightsEnd == null,
            fightLogIds
        });
    }

    private static async Task<IResult> GetAggregate(
        long guildId,
        ClaimsPrincipal user,
        [FromServices] IUserGuildsService userGuilds,
        [FromServices] IRaidLifecycleService raidLifecycle,
        [FromServices] IDbContextFactory<DatabaseContext> dbContextFactory,
        HttpContext httpContext,
        string? logIds = null)
    {
        if (await EnsureAuthorizedAsync(user, guildId, userGuilds) is { } denied)
        {
            return denied;
        }

        var report = await raidLifecycle.GetLatestRaidAsync(guildId);
        if (report == null)
        {
            return Results.NotFound();
        }

        await using var ctx = await dbContextFactory.CreateDbContextAsync();
        var reportFightIds = await GetFightLogIdsInWindowAsync(ctx, report);
        if (reportFightIds.Count == 0)
        {
            return Results.NotFound();
        }

        var fightLogIds = reportFightIds;
        var logIdsParamPresent = httpContext.Request.Query.ContainsKey("logIds");
        if (logIdsParamPresent)
        {
            var requested = (logIds ?? string.Empty).Split(',')
                .Select(s => long.TryParse(s.Trim(), out var v) ? (long?)v : null)
                .Where(v => v.HasValue)
                .Select(v => v!.Value)
                .ToHashSet();
            fightLogIds = reportFightIds.Where(id => requested.Contains(id)).ToList();
            if (fightLogIds.Count == 0)
            {
                return Results.NotFound();
            }
        }

        return await LogsEndpoints.AggregateLogsCoreAsync(fightLogIds, ctx);
    }

    private static async Task<IResult> GetSingleLog(
        long guildId,
        long fightLogId,
        ClaimsPrincipal user,
        [FromServices] IUserGuildsService userGuilds,
        [FromServices] IRaidLifecycleService raidLifecycle,
        [FromServices] IDbContextFactory<DatabaseContext> dbContextFactory)
    {
        if (await EnsureAuthorizedAsync(user, guildId, userGuilds) is { } denied)
        {
            return denied;
        }

        var report = await raidLifecycle.GetLatestRaidAsync(guildId);
        if (report == null)
        {
            return Results.NotFound();
        }

        await using var ctx = await dbContextFactory.CreateDbContextAsync();

        var end = report.FightsEnd ?? DateTime.UtcNow;
        var inWindow = await ctx.FightLog.AnyAsync(fl =>
            fl.FightLogId == fightLogId
            && fl.GuildId == guildId
            && fl.FightStart >= report.FightsStart
            && fl.FightStart <= end);
        if (!inWindow)
        {
            return Results.NotFound();
        }

        return await LogsEndpoints.GetLogCoreAsync(fightLogId, ctx);
    }

    private static async Task StreamUpdates(
        long guildId,
        ClaimsPrincipal user,
        [FromServices] IUserGuildsService userGuilds,
        [FromServices] IRaidLifecycleService raidLifecycle,
        [FromServices] IDbContextFactory<DatabaseContext> dbContextFactory,
        [FromServices] IHostApplicationLifetime lifetime,
        HttpContext httpContext,
        CancellationToken ct)
    {
        // Combine the request-aborted token with the app's stopping token so app shutdown
        // doesn't wait for Kestrel's drain timeout to tear down active SSE connections.
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, lifetime.ApplicationStopping);
        ct = linkedCts.Token;

        if (!await userGuilds.IsMemberAsync(user, (ulong)guildId, ct))
        {
            httpContext.Response.StatusCode = StatusCodes.Status403Forbidden;
            return;
        }

        httpContext.Response.ContentType = "text/event-stream";
        httpContext.Response.Headers["Cache-Control"] = "no-cache";
        httpContext.Response.Headers["X-Accel-Buffering"] = "no";

        long lastReportId = 0;
        var knownFightIds = new HashSet<long>();
        var lastWasOpen = false;
        var pollInterval = TimeSpan.FromSeconds(2);
        var heartbeatInterval = TimeSpan.FromSeconds(20);
        var lastHeartbeat = DateTime.UtcNow;

        try
        {
            while (!ct.IsCancellationRequested)
            {
                var report = await raidLifecycle.GetLatestRaidAsync(guildId, ct);
                if (report != null)
                {
                    if (report.FightsReportId != lastReportId)
                    {
                        lastReportId = report.FightsReportId;
                        knownFightIds.Clear();
                        await WriteEvent(httpContext, "report-changed", new
                        {
                            reportId = report.FightsReportId,
                            fightsStart = report.FightsStart,
                            fightsEnd = report.FightsEnd,
                            isOpen = report.FightsEnd == null
                        }, ct);
                        lastWasOpen = report.FightsEnd == null;
                    }

                    await using var ctx = await dbContextFactory.CreateDbContextAsync(ct);
                    var fightLogIds = await GetFightLogIdsInWindowAsync(ctx, report, ct);
                    foreach (var id in fightLogIds)
                    {
                        if (knownFightIds.Add(id))
                        {
                            await WriteEvent(httpContext, "fight-added", new { fightLogId = id }, ct);
                        }
                    }

                    var isOpenNow = report.FightsEnd == null;
                    if (lastWasOpen && !isOpenNow)
                    {
                        await WriteEvent(httpContext, "closed", new
                        {
                            reportId = report.FightsReportId,
                            fightsEnd = report.FightsEnd
                        }, ct);
                    }
                    lastWasOpen = isOpenNow;
                }

                if (DateTime.UtcNow - lastHeartbeat >= heartbeatInterval)
                {
                    await httpContext.Response.WriteAsync(": heartbeat\n\n", ct);
                    await httpContext.Response.Body.FlushAsync(ct);
                    lastHeartbeat = DateTime.UtcNow;
                }

                await Task.Delay(pollInterval, ct);
            }
        }
        catch (OperationCanceledException)
        {
            // Client disconnected or request aborted; exit cleanly.
        }
        catch (IOException)
        {
            // Underlying connection went away mid-write; nothing to do.
        }
    }

    private static async Task WriteEvent(HttpContext httpContext, string eventName, object payload, CancellationToken ct)
    {
        var json = JsonSerializer.Serialize(payload);
        await httpContext.Response.WriteAsync($"event: {eventName}\ndata: {json}\n\n", ct);
        await httpContext.Response.Body.FlushAsync(ct);
    }

    private static async Task<IResult> StartRaid(
        long guildId,
        ClaimsPrincipal user,
        [FromServices] IUserGuildsService userGuilds,
        [FromServices] IRaidLifecycleService raidLifecycle,
        [FromServices] IRaidNotifier raidNotifier)
    {
        if (await EnsureAuthorizedAsync(user, guildId, userGuilds) is { } denied)
        {
            return denied;
        }

        var result = await raidLifecycle.OpenRaidAsync(guildId);
        if (result.Outcome == RaidOpenOutcome.Opened)
        {
            await raidNotifier.PostRaidStartedAsync(guildId);
        }

        return result.Outcome switch
        {
            RaidOpenOutcome.GuildNotConfigured => Results.BadRequest(new { error = "This server doesn't have raids enabled." }),
            RaidOpenOutcome.AlreadyOpen => Results.Conflict(new { error = "A raid is already open for this server." }),
            RaidOpenOutcome.Opened => Results.Ok(new
            {
                reportId = result.Report!.FightsReportId,
                fightsStart = result.Report.FightsStart
            }),
            _ => Results.StatusCode(500)
        };
    }

    private static async Task<IResult> StopRaid(
        long guildId,
        ClaimsPrincipal user,
        [FromServices] IUserGuildsService userGuilds,
        [FromServices] IRaidLifecycleService raidLifecycle,
        [FromServices] IRaidNotifier raidNotifier)
    {
        if (await EnsureAuthorizedAsync(user, guildId, userGuilds) is { } denied)
        {
            return denied;
        }

        var result = await raidLifecycle.CloseRaidAsync(guildId);
        if (result.Outcome == RaidCloseOutcome.Closed && result.Report != null)
        {
            await raidNotifier.PostRaidEndedAsync(result.Report);
        }

        return result.Outcome switch
        {
            RaidCloseOutcome.NoneOpen => Results.NotFound(new { error = "No open raid to close." }),
            RaidCloseOutcome.Closed => Results.Ok(new
            {
                reportId = result.Report!.FightsReportId,
                fightsEnd = result.Report.FightsEnd
            }),
            _ => Results.StatusCode(500)
        };
    }

    private static async Task<IResult?> EnsureAuthorizedAsync(
        ClaimsPrincipal user, long guildId, IUserGuildsService userGuilds)
    {
        return await userGuilds.IsMemberAsync(user, (ulong)guildId) ? null : Results.Forbid();
    }

    private static async Task<List<long>> GetFightLogIdsInWindowAsync(
        DatabaseContext ctx, FightsReport report, CancellationToken ct = default)
    {
        var end = report.FightsEnd ?? DateTime.UtcNow;
        return await ctx.FightLog
            .Where(fl => fl.GuildId == report.GuildId
                && fl.FightStart >= report.FightsStart
                && fl.FightStart <= end)
            .OrderBy(fl => fl.FightStart)
            .Select(fl => fl.FightLogId)
            .ToListAsync(ct);
    }
}
