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

        var joined = from pfl in context.PlayerFightLog
                     join fl in context.FightLog on pfl.FightLogId equals fl.FightLogId
                     where gw2Names.Contains(pfl.GuildWarsAccountName)
                     select new { Pfl = pfl, fl.FightType, fl.FightStart };

        var totals = await joined
            .GroupBy(_ => 1)
            .Select(g => new
            {
                Total = g.Count(),
                Wvw = g.Count(x => x.FightType == 0),
                Pve = g.Count(x => x.FightType != 0),
                TotalDamage = g.Sum(x => x.Pfl.Damage),
                TotalKills = g.Sum(x => x.FightType == 0 ? x.Pfl.Kills : 0),
                TotalDeaths = g.Sum(x => x.Pfl.Deaths),
                TotalHealing = g.Sum(x => x.Pfl.Healing),
                TotalCleanses = g.Sum(x => x.Pfl.Cleanses),
                TotalStrips = g.Sum(x => x.Pfl.Strips),
                TotalDownContribution = g.Sum(x => x.FightType == 0 ? x.Pfl.DamageDownContribution : 0L),
                AvgQuickness = g.Average(x => (double)x.Pfl.QuicknessDuration),
                AvgAlac = g.Average(x => (double)x.Pfl.AlacDuration),
                LastFightDate = (DateTime?)g.Max(x => x.FightStart)
            })
            .FirstOrDefaultAsync();

        if (totals is null)
        {
            return Results.Ok(new { account, gw2Accounts, lastFightDate = (DateTime?)null, fights = (object?)null });
        }

        var bestDamageFight = await joined
            .OrderByDescending(x => x.Pfl.Damage)
            .Select(x => new { fightLogId = x.Pfl.FightLogId, fightType = x.FightType, damage = x.Pfl.Damage })
            .FirstOrDefaultAsync();

        var bestKillsFight = await joined
            .Where(x => x.FightType == 0)
            .OrderByDescending(x => x.Pfl.Kills)
            .Select(x => new { fightLogId = x.Pfl.FightLogId, kills = x.Pfl.Kills })
            .FirstOrDefaultAsync();

        var characterCount = await joined
            .Where(x => x.Pfl.CharacterName != null && x.Pfl.CharacterName != "")
            .Select(x => x.Pfl.CharacterName)
            .Distinct()
            .CountAsync();

        var fights = new
        {
            total = totals.Total,
            wvw = totals.Wvw,
            pve = totals.Pve,
            totalDamage = totals.TotalDamage,
            totalKills = totals.TotalKills,
            totalDeaths = totals.TotalDeaths,
            totalHealing = totals.TotalHealing,
            totalCleanses = totals.TotalCleanses,
            totalStrips = totals.TotalStrips,
            totalDownContribution = totals.TotalDownContribution,
            avgQuickness = totals.AvgQuickness,
            avgAlac = totals.AvgAlac,
            bestDamageFight,
            bestKillsFight
        };

        return Results.Ok(new { account, gw2Accounts, lastFightDate = totals.LastFightDate, fights, characterCount });
    }
}
