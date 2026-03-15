using DonBot.Models.Entities;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace DonBot.Api.Endpoints;

public static class StatsEndpoints
{
    public static void MapStatsEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/stats").RequireAuthorization();
        group.MapGet("/me", GetMyStats);
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
            return Results.Ok(new { totalFights = 0 });
        }

        return Results.Ok(new
        {
            totalFights = playerLogs.Count,
            totalDamage = playerLogs.Sum(p => p.Damage),
            totalDeaths = playerLogs.Sum(p => p.Deaths),
            totalHealing = playerLogs.Sum(p => p.Healing),
            totalCleanses = playerLogs.Sum(p => p.Cleanses),
            totalStrips = playerLogs.Sum(p => p.Strips),
            avgQuickness = playerLogs.Average(p => (double)p.QuicknessDuration),
            avgAlac = playerLogs.Average(p => (double)p.AlacDuration)
        });
    }
}
