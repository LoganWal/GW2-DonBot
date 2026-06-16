using DonBot.Core.Models.Entities;
using DonBot.Core.Models.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DonBot.Services.GuildWarsServices;

public sealed class PointsAwardService(
    IDbContextFactory<DatabaseContext> dbContextFactory,
    ILogger<PointsAwardService> logger) : IPointsAwardService
{
    private const decimal MaxPointsPerFight = 16m;
    private const decimal FirstComponentPoints = 8m;
    private const double Percentile = 0.95;
    private const double AnomalyPercentile = 0.99;
    private const double OutlierMultiple = 3.0;

    private static readonly MetricDefinition[] Metrics =
    [
        new("dps", "DPS", (player, fight) => PerSecond(player.Damage, fight), 10, true, false),
        new("cleanses", "Cleanses", (player, _) => player.Cleanses, 12, false, true),
        new("strips", "Strips", (player, _) => player.Strips, 12, false, true),
        new("stability", "Stability", (player, _) => (double)(player.StabGenOnGroup + player.StabGenOffGroup), 10, true, false),
        new("hps", "Healing/sec", (player, fight) => PerSecond(player.Healing, fight), 10, true, false),
        new("barrier", "Barrier/sec", (player, fight) => PerSecond(player.BarrierGenerated, fight), 10, true, false)
    ];

    public async Task<IReadOnlyList<PlayerPointAward>> AwardFightAsync(long fightLogId, CancellationToken ct = default)
    {
        await using var ctx = await dbContextFactory.CreateDbContextAsync(ct);
        await using var tx = await ctx.Database.BeginTransactionAsync(ct);

        var fight = await ctx.FightLog.AsTracking().FirstOrDefaultAsync(f => f.FightLogId == fightLogId, ct);
        if (fight is null || IsExcludedFightType(fight.FightType))
        {
            return [];
        }

        var playerLogs = await ctx.PlayerFightLog
            .Where(p => p.FightLogId == fightLogId)
            .ToListAsync(ct);
        if (playerLogs.Count == 0)
        {
            return [];
        }

        var playerFightLogIds = playerLogs.Select(p => p.PlayerFightLogId).ToList();
        var alreadyAwardedPlayerIds = await ctx.PlayerPointAward
            .Where(a => playerFightLogIds.Contains(a.PlayerFightLogId))
            .Select(a => a.PlayerFightLogId)
            .Distinct()
            .ToListAsync(ct);

        var alreadyAwarded = alreadyAwardedPlayerIds.ToHashSet();
        var awardablePlayerLogs = playerLogs
            .Where(p => !alreadyAwarded.Contains(p.PlayerFightLogId))
            .ToList();
        if (awardablePlayerLogs.Count == 0)
        {
            return [];
        }

        var accountNames = awardablePlayerLogs
            .Select(p => p.GuildWarsAccountName)
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (accountNames.Count == 0)
        {
            return [];
        }

        var normalizedAccountNames = accountNames
            .Select(n => n.ToUpperInvariant())
            .ToList();

        var gw2Accounts = await ctx.GuildWarsAccount
            .Where(a => a.GuildWarsAccountName != null && normalizedAccountNames.Contains(a.GuildWarsAccountName.ToUpper()))
            .ToListAsync(ct);
        var discordIdByGw2Account = gw2Accounts
            .Where(a => !string.IsNullOrWhiteSpace(a.GuildWarsAccountName))
            .GroupBy(a => a.GuildWarsAccountName!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First().DiscordId, StringComparer.OrdinalIgnoreCase);

        var discordIds = discordIdByGw2Account.Values.Distinct().ToList();
        var accounts = await ctx.Account
            .AsTracking()
            .Where(a => discordIds.Contains(a.DiscordId))
            .ToDictionaryAsync(a => a.DiscordId, ct);
        if (accounts.Count == 0)
        {
            return [];
        }

        var referenceRows = await (
            from player in ctx.PlayerFightLog
            join referenceFight in ctx.FightLog on player.FightLogId equals referenceFight.FightLogId
            where referenceFight.FightType == fight.FightType
                && referenceFight.FightLogId != fight.FightLogId
                && (referenceFight.FightType == (short)FightTypesEnum.WvW || referenceFight.IsSuccess)
            select new ReferenceRow(player, referenceFight))
            .ToListAsync(ct);

        var thresholds = BuildThresholds(referenceRows);
        if (thresholds.Count == 0)
        {
            return [];
        }

        var now = DateTime.UtcNow;
        var awards = new List<PlayerPointAward>();
        var fullCreditFight = fight.FightType == (short)FightTypesEnum.WvW || fight.IsSuccess;

        foreach (var playerLog in awardablePlayerLogs)
        {
            if (!discordIdByGw2Account.TryGetValue(playerLog.GuildWarsAccountName, out var discordId))
            {
                continue;
            }
            if (!accounts.ContainsKey(discordId))
            {
                continue;
            }

            var earnedMetrics = GetEarnedMetrics(playerLog, fight, thresholds, fullCreditFight);
            if (earnedMetrics.Count == 0)
            {
                continue;
            }

            var remaining = MaxPointsPerFight;
            for (var i = 0; i < earnedMetrics.Count && remaining > 0m; i++)
            {
                var earned = earnedMetrics[i];
                var basePoints = FirstComponentPoints / (decimal)Math.Pow(2, i);
                var points = Math.Round(Math.Min(remaining, basePoints * earned.Multiplier), 3);
                if (points <= 0m)
                {
                    continue;
                }

                awards.Add(new PlayerPointAward
                {
                    FightLogId = fight.FightLogId,
                    PlayerFightLogId = playerLog.PlayerFightLogId,
                    DiscordId = discordId,
                    GuildWarsAccountName = playerLog.GuildWarsAccountName,
                    FightType = fight.FightType,
                    Metric = earned.Metric.Key,
                    MetricLabel = earned.Metric.Label,
                    MetricValue = ToDecimal(earned.Value),
                    PercentileValue = ToDecimal(earned.PercentileValue),
                    BasePoints = Math.Round(basePoints, 3),
                    Multiplier = earned.Multiplier,
                    Points = points,
                    Reason = earned.Reason,
                    AwardedAt = now
                });
                remaining -= points;
            }
        }

        if (awards.Count == 0)
        {
            return [];
        }

        foreach (var group in awards.GroupBy(a => a.DiscordId))
        {
            if (!accounts.TryGetValue(group.Key, out var account))
            {
                continue;
            }

            var total = group.Sum(a => a.Points);
            account.PreviousPoints = account.Points;
            account.Points += total;
            account.AvailablePoints += total;
            if (fight.FightType == (short)FightTypesEnum.WvW)
            {
                account.LastWvwLogDateTime = now;
            }
        }

        ctx.PlayerPointAward.AddRange(awards);

        try
        {
            await ctx.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        }
        catch (DbUpdateException ex)
        {
            logger.LogWarning(ex, "Failed to award points for fight log {FightLogId}.", fightLogId);
            await tx.RollbackAsync(ct);
            return [];
        }

        return awards;
    }

    private static Dictionary<string, MetricThreshold> BuildThresholds(List<ReferenceRow> referenceRows)
    {
        var thresholds = new Dictionary<string, MetricThreshold>();
        foreach (var metric in Metrics)
        {
            var values = referenceRows
                .Select(r => metric.Value(r.Player, r.Fight))
                .Where(v => double.IsFinite(v) && (metric.IncludeZeroReferenceValues ? v >= 0 : v > 0))
                .Order()
                .ToList();
            if (values.Count == 0)
            {
                continue;
            }

            thresholds[metric.Key] = new MetricThreshold(
                CalculatePercentile(values, Percentile),
                CalculatePercentile(values, AnomalyPercentile));
        }

        return thresholds;
    }

    private static List<EarnedMetric> GetEarnedMetrics(
        PlayerFightLog playerLog,
        FightLog fight,
        IReadOnlyDictionary<string, MetricThreshold> thresholds,
        bool fullCreditFight)
    {
        var earned = new List<EarnedMetric>();
        foreach (var metric in Metrics)
        {
            if (!thresholds.TryGetValue(metric.Key, out var threshold))
            {
                continue;
            }

            var percentileValue = threshold.Percentile95;
            var value = metric.Value(playerLog, fight);
            if (!double.IsFinite(value) || value <= 0)
            {
                continue;
            }

            var ratio = percentileValue > 0 ? value / percentileValue : value;
            var multiplier = Math.Min(ToDecimal(ratio), 1m);
            var reason = "95th percentile";

            if (percentileValue < metric.MinimumRelevantValue)
            {
                if (!metric.AllowLowBenchmarkOutlier ||
                    value < metric.MinimumRelevantValue ||
                    !IsSubstantiallyHigher(value, percentileValue))
                {
                    continue;
                }

                multiplier = 0.5m;
                reason = "Outlier half credit";
            }
            else if (!fullCreditFight && IsDetectedAnomaly(value, threshold.Percentile99))
            {
                multiplier *= 0.5m;
                reason = "Detected anomaly half credit";
            }
            else if (value < percentileValue)
            {
                reason = "Scaled to 95th percentile";
            }

            earned.Add(new EarnedMetric(metric, value, percentileValue, ratio, multiplier, reason));
        }

        return earned
            .OrderByDescending(e => e.Ratio)
            .ThenByDescending(e => e.Value)
            .ToList();
    }

    private static bool IsExcludedFightType(short fightType) =>
        fightType is (short)FightTypesEnum.Unkn or (short)FightTypesEnum.Golem;

    private static bool IsSubstantiallyHigher(double value, double percentileValue) =>
        value >= Math.Max(percentileValue, 1d) * OutlierMultiple;

    private static bool IsDetectedAnomaly(double value, double percentile99) =>
        double.IsFinite(percentile99) && percentile99 > 0d && value > percentile99;

    private static double CalculatePercentile(IReadOnlyList<double> sortedValues, double percentile)
    {
        if (sortedValues.Count == 1)
        {
            return sortedValues[0];
        }

        var position = (sortedValues.Count - 1) * percentile;
        var lower = (int)Math.Floor(position);
        var upper = (int)Math.Ceiling(position);
        if (lower == upper)
        {
            return sortedValues[lower];
        }

        var weight = position - lower;
        return sortedValues[lower] + (sortedValues[upper] - sortedValues[lower]) * weight;
    }

    private static double PerSecond(long value, FightLog fight)
    {
        var seconds = Math.Max(fight.FightDurationInMs / 1000.0, 1.0);
        return value / seconds;
    }

    private static decimal ToDecimal(double value) =>
        double.IsFinite(value) ? Math.Round((decimal)value, 3) : 0m;

    private sealed record MetricDefinition(
        string Key,
        string Label,
        Func<PlayerFightLog, FightLog, double> Value,
        double MinimumRelevantValue,
        bool AllowLowBenchmarkOutlier,
        bool IncludeZeroReferenceValues);

    private sealed record ReferenceRow(PlayerFightLog Player, FightLog Fight);

    private sealed record MetricThreshold(double Percentile95, double Percentile99);

    private sealed record EarnedMetric(
        MetricDefinition Metric,
        double Value,
        double PercentileValue,
        double Ratio,
        decimal Multiplier,
        string Reason);
}
