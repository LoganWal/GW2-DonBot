using DonBot.Models.Entities;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace DonBot.Api.Endpoints;

public static class PointsEndpoints
{
    public static void MapPointsEndpoints(this WebApplication app)
    {
        var pointsGroup = app.MapGroup("/api/points").RequireAuthorization();
        pointsGroup.MapGet("/me", GetMyPoints);

        var rafflesGroup = app.MapGroup("/api/raffles").RequireAuthorization();
        rafflesGroup.MapGet("/", GetRaffles);

        var dashboardGroup = app.MapGroup("/api/dashboard").RequireAuthorization();
        dashboardGroup.MapGet("/", GetDashboard);
    }

    private static async Task<IResult> GetMyPoints(
        ClaimsPrincipal user,
        IDbContextFactory<DatabaseContext> dbContextFactory)
    {
        var discordIdStr = user.FindFirst("discord_id")?.Value;
        if (!long.TryParse(discordIdStr, out var discordId))
        {
            return Results.Unauthorized();
        }

        await using var context = await dbContextFactory.CreateDbContextAsync();
        var account = await context.Account.FirstOrDefaultAsync(a => a.DiscordId == discordId);
        if (account is null)
        {
            return Results.NotFound();
        }

        return Results.Ok(account);
    }

    private static async Task<IResult> GetRaffles(
        ClaimsPrincipal user,
        IDbContextFactory<DatabaseContext> dbContextFactory)
    {
        var discordIdStr = user.FindFirst("discord_id")?.Value;
        if (!long.TryParse(discordIdStr, out var discordId))
        {
            return Results.Unauthorized();
        }

        await using var context = await dbContextFactory.CreateDbContextAsync();

        var activeRaffles = await context.Raffle
            .Where(r => r.IsActive)
            .ToListAsync();

        var raffleIds = activeRaffles.Select(r => r.Id).ToList();

        var userBids = await context.PlayerRaffleBid
            .Where(b => b.DiscordId == discordId && raffleIds.Contains(b.RaffleId))
            .ToListAsync();

        return Results.Ok(new { raffles = activeRaffles, userBids });
    }

    private static async Task<IResult> GetDashboard(
        ClaimsPrincipal user,
        IDbContextFactory<DatabaseContext> dbContextFactory)
    {
        var discordIdStr = user.FindFirst("discord_id")?.Value;
        if (!long.TryParse(discordIdStr, out var discordId))
        {
            return Results.Unauthorized();
        }

        await using var context = await dbContextFactory.CreateDbContextAsync();

        var account = await context.Account.FirstOrDefaultAsync(a => a.DiscordId == discordId);

        var gw2Accounts = await context.GuildWarsAccount
            .Where(a => a.DiscordId == discordId)
            .ToListAsync();

        var gw2Names = gw2Accounts
            .Select(a => a.GuildWarsAccountName)
            .Where(n => n != null)
            .ToList();

        DateTime? lastFightDate = null;
        if (gw2Names.Count > 0)
        {
            var lastFightLogId = await context.PlayerFightLog
                .Where(pfl => gw2Names.Contains(pfl.GuildWarsAccountName))
                .OrderByDescending(pfl => pfl.FightLogId)
                .Select(pfl => (long?)pfl.FightLogId)
                .FirstOrDefaultAsync();

            if (lastFightLogId.HasValue)
            {
                lastFightDate = await context.FightLog
                    .Where(fl => fl.FightLogId == lastFightLogId.Value)
                    .Select(fl => (DateTime?)fl.FightStart)
                    .FirstOrDefaultAsync();
            }
        }

        return Results.Ok(new { account, gw2Accounts, lastFightDate });
    }
}
