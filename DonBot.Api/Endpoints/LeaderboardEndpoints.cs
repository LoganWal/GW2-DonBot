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

        var userFightLogIds = await context.PlayerFightLog
            .Where(pfl => gw2Names.Contains(pfl.GuildWarsAccountName))
            .Select(pfl => pfl.FightLogId)
            .Distinct()
            .ToListAsync();

        var candidateGuildIds = await context.FightLog
            .Where(fl => userFightLogIds.Contains(fl.FightLogId))
            .Select(fl => fl.GuildId)
            .Distinct()
            .ToListAsync();

        var guildData = await context.Guild
            .Where(g => candidateGuildIds.Contains(g.GuildId))
            .Select(g => new { guildId = g.GuildId.ToString(), g.GuildName })
            .ToListAsync();

        return Results.Ok(guildData);
    }

    private static async Task<IResult> GetLeaderboard(
        long guildId,
        IDbContextFactory<DatabaseContext> dbContextFactory,
        int days = 7)
    {
        await using var context = await dbContextFactory.CreateDbContextAsync();

        var since = DateTime.UtcNow.AddDays(-days);

        var query = context.PlayerFightLog
            .Join(context.FightLog,
                pfl => pfl.FightLogId,
                fl => fl.FightLogId,
                (pfl, fl) => new { pfl, fl })
            .Where(x => x.fl.FightStart >= since);

        if (guildId != -1)
        {
            query = query.Where(x => x.fl.GuildId == guildId);
        }

        var rows = await query
            .Select(x => new
            {
                x.pfl.GuildWarsAccountName,
                x.fl.FightType,
                x.fl.FightDurationInMs,
                x.pfl.Damage,
                x.pfl.Cleave,
                x.pfl.DamageDownContribution,
                x.pfl.Kills,
                x.pfl.Downs,
                x.pfl.Deaths,
                x.pfl.TimesDowned,
                x.pfl.Healing,
                x.pfl.Cleanses,
                x.pfl.Strips,
                x.pfl.NumberOfBoonsRipped,
                x.pfl.ResurrectionTime,
                x.pfl.BarrierGenerated,
                x.pfl.QuicknessDuration,
                x.pfl.AlacDuration,
            })
            .ToListAsync();

        var wvwRows = rows.Where(r => r.FightType == 0).ToList();
        var pveRows = rows.Where(r => r.FightType != 0).ToList();

        var wvw = wvwRows
            .GroupBy(r => r.GuildWarsAccountName)
            .Select(g =>
            {
                var fights = g.ToList();
                return new
                {
                    accountName = g.Key,
                    fights = fights.Count,
                    avgDamage = (long)Math.Round(fights.Average(f => (double)f.Damage), 0),
                    avgDdc = (long)Math.Round(fights.Average(f => (double)f.DamageDownContribution), 0),
                    avgKills = Math.Round(fights.Average(f => (double)f.Kills), 2),
                    avgDowns = Math.Round(fights.Average(f => (double)f.Downs), 2),
                    avgDeaths = Math.Round(fights.Average(f => (double)f.Deaths), 2),
                    avgTimesDowned = Math.Round(fights.Average(f => (double)f.TimesDowned), 2),
                    avgHealing = (long)Math.Round(fights.Average(f => (double)f.Healing), 0),
                    avgCleanses = Math.Round(fights.Average(f => (double)f.Cleanses), 2),
                    avgStrips = Math.Round(fights.Average(f => (double)f.Strips), 2),
                    avgBoonsRipped = Math.Round(fights.Average(f => (double)f.NumberOfBoonsRipped), 2),
                    avgBarrier = (long)Math.Round(fights.Average(f => (double)f.BarrierGenerated), 0),
                };
            })
            .OrderByDescending(x => x.avgDamage)
            .ToList();

        var pve = pveRows
            .GroupBy(r => r.GuildWarsAccountName)
            .Select(g =>
            {
                var fights = g.ToList();
                var totalDmg = fights.Sum(f => (long)f.Damage);
                var totalCleave = fights.Sum(f => (long)f.Cleave);
                var totalHealing = fights.Sum(f => (long)f.Healing);
                var totalDurationSec = fights.Sum(f => (double)f.FightDurationInMs) / 1000.0;
                return new
                {
                    accountName = g.Key,
                    fights = fights.Count,
                    dps = totalDurationSec > 0 ? (long)Math.Round(totalDmg / totalDurationSec, 0) : 0L,
                    cleaveDps = totalDurationSec > 0 ? (long)Math.Round(totalCleave / totalDurationSec, 0) : 0L,
                    hps = totalDurationSec > 0 ? (long)Math.Round(totalHealing / totalDurationSec, 0) : 0L,
                    avgDeaths = Math.Round(fights.Average(f => (double)f.Deaths), 2),
                    avgTimesDowned = Math.Round(fights.Average(f => (double)f.TimesDowned), 2),
                    avgResTimeSec = Math.Round(fights.Average(f => (double)f.ResurrectionTime) / 1000.0, 2),
                    avgQuick = Math.Round(fights.Average(f => (double)f.QuicknessDuration), 1),
                    avgAlac = Math.Round(fights.Average(f => (double)f.AlacDuration), 1),
                };
            })
            .OrderByDescending(x => x.dps)
            .ToList();

        return Results.Ok(new
        {
            sinceDate = since.ToString("yyyy-MM-dd"),
            untilDate = DateTime.UtcNow.ToString("yyyy-MM-dd"),
            wvw,
            pve,
        });
    }
}
