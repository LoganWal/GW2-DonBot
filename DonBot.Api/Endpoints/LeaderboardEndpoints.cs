using DonBot.Models.Entities;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace DonBot.Api.Endpoints;

public static class LeaderboardEndpoints
{
    public static void MapLeaderboardEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/guilds").RequireAuthorization();
        group.MapGet("/mine", GetMyGuilds);
        group.MapGet("/{guildId:long}/leaderboard", GetLeaderboard);
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

        var gw2Names = await context.GuildWarsAccount
            .Where(a => a.DiscordId == discordId && !string.IsNullOrEmpty(a.GuildWarsAccountName))
            .Select(a => a.GuildWarsAccountName)
            .ToListAsync();

        if (gw2Names.Count == 0)
        {
            return Results.Ok(Array.Empty<long>());
        }

        // Step 1: FightLog IDs the user personally appeared in
        var userFightLogIds = await context.PlayerFightLog
            .Where(pfl => gw2Names.Contains(pfl.GuildWarsAccountName))
            .Select(pfl => pfl.FightLogId)
            .Distinct()
            .ToListAsync();

        // Step 2: Guild IDs from those specific fight logs
        var candidateGuildIds = await context.FightLog
            .Where(fl => userFightLogIds.Contains(fl.FightLogId))
            .Select(fl => fl.GuildId)
            .Distinct()
            .ToListAsync();

        // Step 3: Filter to only registered guilds
        var guildIds = await context.Guild
            .Where(g => candidateGuildIds.Contains(g.GuildId))
            .Select(g => g.GuildId)
            .ToListAsync();

        return Results.Ok(guildIds);
    }

    private static async Task<IResult> GetLeaderboard(
        long guildId,
        IDbContextFactory<DatabaseContext> dbContextFactory,
        int top = 20)
    {
        await using var context = await dbContextFactory.CreateDbContextAsync();

        var leaderboard = await context.PlayerFightLog
            .Join(context.FightLog,
                pfl => pfl.FightLogId,
                fl => fl.FightLogId,
                (pfl, fl) => new { pfl, fl })
            .Where(x => x.fl.GuildId == guildId)
            .GroupBy(x => x.pfl.GuildWarsAccountName)
            .Select(g => new
            {
                guildWarsAccountName = g.Key,
                totalDamage = g.Sum(x => x.pfl.Damage),
                totalFights = g.Count()
            })
            .OrderByDescending(x => x.totalDamage)
            .Take(top)
            .ToListAsync();

        return Results.Ok(leaderboard);
    }
}
