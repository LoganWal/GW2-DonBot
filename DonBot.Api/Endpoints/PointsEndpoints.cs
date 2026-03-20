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

        if (gw2Names.Count == 0)
        {
            return Results.Ok(new { account, gw2Accounts, lastFightDate = (DateTime?)null, fights = (object?)null });
        }

        var playerLogs = await context.PlayerFightLog
            .Where(pfl => gw2Names.Contains(pfl.GuildWarsAccountName))
            .ToListAsync();

        if (playerLogs.Count == 0)
        {
            return Results.Ok(new { account, gw2Accounts, lastFightDate = (DateTime?)null, fights = (object?)null });
        }

        var fightLogIds = playerLogs.Select(p => p.FightLogId).Distinct().ToList();
        var fightLogs = await context.FightLog
            .Where(fl => fightLogIds.Contains(fl.FightLogId))
            .ToListAsync();

        var fightTypeById = fightLogs.ToDictionary(fl => fl.FightLogId, fl => fl.FightType);
        var fightStartById = fightLogs.ToDictionary(fl => fl.FightLogId, fl => fl.FightStart);

        var wvwLogs = playerLogs.Where(p => fightTypeById.TryGetValue(p.FightLogId, out var t) && t == 0).ToList();
        var pveLogs = playerLogs.Where(p => fightTypeById.TryGetValue(p.FightLogId, out var t) && t != 0).ToList();

        var lastFightDate = fightLogs.Count > 0
            ? fightLogs.Max(fl => fl.FightStart)
            : (DateTime?)null;

        var bestDamageLog = playerLogs.MaxBy(p => p.Damage);
        var bestDamageFight = bestDamageLog is not null && fightTypeById.TryGetValue(bestDamageLog.FightLogId, out var bdt)
            ? new { fightLogId = bestDamageLog.FightLogId, fightType = bdt, damage = bestDamageLog.Damage }
            : null;

        var bestKillsLog = wvwLogs.MaxBy(p => p.Kills);
        var bestKillsFight = bestKillsLog is not null && fightTypeById.TryGetValue(bestKillsLog.FightLogId, out _)
            ? new { fightLogId = bestKillsLog.FightLogId, kills = bestKillsLog.Kills }
            : null;

        var fights = new
        {
            total = playerLogs.Count,
            wvw = wvwLogs.Count,
            pve = pveLogs.Count,
            // Career totals
            totalDamage = playerLogs.Sum(p => p.Damage),
            totalKills = wvwLogs.Sum(p => p.Kills),
            totalDeaths = playerLogs.Sum(p => p.Deaths),
            totalHealing = playerLogs.Sum(p => p.Healing),
            totalCleanses = playerLogs.Sum(p => p.Cleanses),
            totalStrips = playerLogs.Sum(p => p.Strips),
            totalDownContribution = wvwLogs.Sum(p => p.DamageDownContribution),
            // Averages
            avgQuickness = playerLogs.Average(p => (double)p.QuicknessDuration),
            avgAlac = playerLogs.Average(p => (double)p.AlacDuration),
            // Personal bests
            bestDamageFight,
            bestKillsFight
        };

        return Results.Ok(new { account, gw2Accounts, lastFightDate, fights });
    }
}
