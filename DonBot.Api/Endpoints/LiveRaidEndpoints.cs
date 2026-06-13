using System.Security.Claims;
using System.Text.Json;
using DonBot.Api.Services;
using DonBot.Core.Models.Entities;
using DonBot.Core.Services.RaidLifecycle;
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
        group.MapGet("/{guildId:long}/stream", StreamUpdates);
        group.MapPost("/{guildId:long}/start", StartRaid);
        group.MapPost("/{guildId:long}/stop", StopRaid);
    }

    // System.Text.Json uses these response DTO members implicitly.
    // ReSharper disable UnusedAutoPropertyAccessor.Local
    // ReSharper disable UnusedMember.Local
    private sealed class GuildListItem(string guildId, string guildName)
    {
        public string GuildId { get; } = guildId;
        public string GuildName { get; } = guildName;
    }
    // ReSharper restore UnusedMember.Local
    // ReSharper restore UnusedAutoPropertyAccessor.Local

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
                return [];
            }

            var userGuildIds = userGuildList.Select(g => (long)g.Id).ToHashSet();

            await using var ctx = await dbContextFactory.CreateDbContextAsync();
            var guildIdsWithReports = ctx.FightsReport
                .Where(r => r.GuildId > 0)
                .Select(r => r.GuildId)
                .Distinct();

            return await ctx.Guild
                .Join(
                    guildIdsWithReports,
                    guild => guild.GuildId,
                    reportGuildId => reportGuildId,
                    (guild, _) => guild)
                .Where(guild => userGuildIds.Contains(guild.GuildId))
                .OrderBy(guild => guild.GuildName)
                .Select(guild => new GuildListItem(guild.GuildId.ToString(), guild.GuildName ?? guild.GuildId.ToString()))
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
            // Serialize Discord snowflakes as strings to avoid JS precision loss.
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
            fightLogIds = reportFightIds.Where(requested.Contains).ToList();
            if (fightLogIds.Count == 0)
            {
                return Results.NotFound();
            }
        }

        return await LogsEndpoints.AggregateLogsCoreAsync(fightLogIds, ctx);
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
        // Tear down active SSE connections promptly during app shutdown.
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
            // Expected when the client disconnects.
        }
        catch (IOException)
        {
            // Expected when the connection closes while writing.
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
        if (result is { Outcome: RaidCloseOutcome.Closed, Report: not null })
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
