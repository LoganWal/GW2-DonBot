using DonBot.Models.Entities;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace DonBot.Api.Endpoints;

public static class LogsEndpoints
{
    public static void MapLogsEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/logs").RequireAuthorization();
        group.MapGet("/", GetLogs);
        group.MapGet("/{id:long}", GetLog);
    }

    private static async Task<IResult> GetLogs(
        ClaimsPrincipal user,
        IDbContextFactory<DatabaseContext> dbContextFactory,
        long? guildId = null,
        short? fightType = null,
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

        var participatedLogIds = await context.PlayerFightLog
            .Where(pfl => gw2Names.Contains(pfl.GuildWarsAccountName))
            .Select(pfl => pfl.FightLogId)
            .Distinct()
            .ToListAsync();

        var query = context.FightLog
            .Where(fl => participatedLogIds.Contains(fl.FightLogId));

        if (guildId.HasValue)
        {
            query = query.Where(fl => fl.GuildId == guildId.Value);
        }

        if (fightType.HasValue)
        {
            query = query.Where(fl => fl.FightType == fightType.Value);
        }

        var total = await query.CountAsync();
        var logs = await query
            .OrderByDescending(fl => fl.FightStart)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return Results.Ok(new { total, page, pageSize, data = logs });
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

        return Results.Ok(new { log, players = playerLogs });
    }
}
