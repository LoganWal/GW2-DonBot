using DonBot.Core.Models.Entities;
using DonBot.Core.Models.GuildWars2;

namespace DonBot.Core.Services;

public static class PlayerFightLogPlaystyleClassifier
{
    public const string DpsPlaystyle = "dps";
    public const string BoonDpsPlaystyle = "boon-dps";
    public const string BoonHealerPlaystyle = "boon-healer";
    public const string MechanicPlaystyle = "mechanic";
    public const string WvwSupportDpsPlaystyle = "support-dps";
    public const string WvwSupportPlaystyle = "support";
    public const string WvwHealSupportPlaystyle = "heal-support";

    public static readonly PlaystyleDefinition[] PvePlaystyles =
    [
        new(DpsPlaystyle, "DPS"),
        new(BoonDpsPlaystyle, "Boon DPS"),
        new(BoonHealerPlaystyle, "Boon Healer"),
        new(MechanicPlaystyle, "Mechanic")
    ];

    public static readonly PlaystyleDefinition[] WvwPlaystyles =
    [
        new(DpsPlaystyle, "DPS"),
        new(WvwSupportDpsPlaystyle, "Support DPS"),
        new(WvwSupportPlaystyle, "Support"),
        new(WvwHealSupportPlaystyle, "Heal Support")
    ];

    private const double DecentPercentile = 0.50;
    private const double HighPercentile = 0.75;
    private const double SupportPercentile = 0.60;

    private const double MinimumDamagePerSecond = 100d;
    private const double MinimumHealingPerSecond = 250d;
    private const double MinimumGoodHealingPerSecond = 500d;
    private const double MinimumCleansesPerMinute = 3d;
    private const double MinimumStripsPerMinute = 1d;

    public static string ResolvePvePlaystyle(string boonRole) =>
        boonRole switch
        {
            PlayerFightLogRoleClassifier.BoonDpsRole => BoonDpsPlaystyle,
            PlayerFightLogRoleClassifier.BoonHealerRole => BoonHealerPlaystyle,
            _ => DpsPlaystyle
        };

    public static string ResolvePvePlaystyle(Gw2Player player, long fightDurationInMs, double averageGroupDps)
    {
        var boonRole = PlayerFightLogRoleClassifier.ResolveBoonRole(player, fightDurationInMs, averageGroupDps);
        if (!string.IsNullOrWhiteSpace(boonRole))
        {
            return ResolvePvePlaystyle(boonRole);
        }

        return PlayerFightLogRoleClassifier.IsLowDps(player, fightDurationInMs, averageGroupDps)
            ? MechanicPlaystyle
            : DpsPlaystyle;
    }

    public static string GetLabel(string playstyle) =>
        PvePlaystyles.Concat(WvwPlaystyles)
            .FirstOrDefault(p => p.Key == playstyle)?.Label ?? playstyle;

    public static bool IsKnownPlaystyle(string playstyle) =>
        PvePlaystyles.Concat(WvwPlaystyles).Any(p => p.Key == playstyle);

    public static bool IsPvePlaystyle(string playstyle) =>
        PvePlaystyles.Any(p => p.Key == playstyle);

    public static bool IsWvwPlaystyle(string playstyle) =>
        WvwPlaystyles.Any(p => p.Key == playstyle);

    public static WvwPlaystyleBenchmarks BuildWvwBenchmarks(
        IEnumerable<PlayerFightLog> logs,
        IReadOnlyDictionary<long, long> durationByFightLogId)
    {
        var metrics = logs
            .Select(log => GetWvwMetrics(log, GetDuration(durationByFightLogId, log.FightLogId)))
            .ToList();

        return new WvwPlaystyleBenchmarks(
            Percentile(metrics.Select(m => m.DamagePerSecond), HighPercentile),
            Percentile(metrics.Select(m => m.DamagePerSecond), DecentPercentile),
            Percentile(metrics.Select(m => m.HealingPerSecond), SupportPercentile),
            Percentile(metrics.Select(m => m.HealingPerSecond), HighPercentile),
            Percentile(metrics.Select(m => m.CleansesPerMinute), SupportPercentile),
            Percentile(metrics.Select(m => m.StripsPerMinute), SupportPercentile),
            Percentile(metrics.Select(m => m.StabilityGeneration), SupportPercentile));
    }

    public static WvwPlaystyleBenchmarks BuildWvwBenchmarks(IEnumerable<Gw2Player> players, long fightDurationInMs)
    {
        var metrics = players
            .Select(player => GetWvwMetrics(player, fightDurationInMs))
            .ToList();

        return new WvwPlaystyleBenchmarks(
            Percentile(metrics.Select(m => m.DamagePerSecond), HighPercentile),
            Percentile(metrics.Select(m => m.DamagePerSecond), DecentPercentile),
            Percentile(metrics.Select(m => m.HealingPerSecond), SupportPercentile),
            Percentile(metrics.Select(m => m.HealingPerSecond), HighPercentile),
            Percentile(metrics.Select(m => m.CleansesPerMinute), SupportPercentile),
            Percentile(metrics.Select(m => m.StripsPerMinute), SupportPercentile),
            Percentile(metrics.Select(m => m.StabilityGeneration), SupportPercentile));
    }

    public static string ResolvePlaystyle(
        PlayerFightLog log,
        bool isWvW,
        long fightDurationInMs,
        WvwPlaystyleBenchmarks? wvwBenchmarks = null)
    {
        if (!string.IsNullOrWhiteSpace(log.Playstyle))
        {
            return log.Playstyle;
        }

        if (!isWvW)
        {
            return ResolvePvePlaystyle(log.BoonRole);
        }

        return wvwBenchmarks is null
            ? DpsPlaystyle
            : ResolveWvwPlaystyle(log, fightDurationInMs, wvwBenchmarks);
    }

    public static string ResolvePlaystyle(
        Gw2Player player,
        bool isWvW,
        long fightDurationInMs,
        double averageGroupDps,
        WvwPlaystyleBenchmarks? wvwBenchmarks = null)
    {
        if (!isWvW)
        {
            return ResolvePvePlaystyle(player, fightDurationInMs, averageGroupDps);
        }

        return wvwBenchmarks is null
            ? DpsPlaystyle
            : ResolveWvwPlaystyle(player, fightDurationInMs, wvwBenchmarks);
    }

    public static string ResolveWvwPlaystyle(
        PlayerFightLog log,
        long fightDurationInMs,
        WvwPlaystyleBenchmarks benchmarks)
    {
        var metrics = GetWvwMetrics(log, fightDurationInMs);
        return ResolveWvwPlaystyle(metrics, benchmarks);
    }

    public static string ResolveWvwPlaystyle(
        Gw2Player player,
        long fightDurationInMs,
        WvwPlaystyleBenchmarks benchmarks)
    {
        var metrics = GetWvwMetrics(player, fightDurationInMs);
        return ResolveWvwPlaystyle(metrics, benchmarks);
    }

    private static string ResolveWvwPlaystyle(WvwMetrics metrics, WvwPlaystyleBenchmarks benchmarks)
    {
        var hasDecentDamage = Qualifies(metrics.DamagePerSecond, benchmarks.DecentDamagePerSecond, MinimumDamagePerSecond);
        var hasDecentSupport = Qualifies(metrics.HealingPerSecond, benchmarks.DecentHealingPerSecond, MinimumHealingPerSecond) ||
            Qualifies(metrics.CleansesPerMinute, benchmarks.DecentCleansesPerMinute, MinimumCleansesPerMinute) ||
            Qualifies(metrics.StripsPerMinute, benchmarks.DecentStripsPerMinute, MinimumStripsPerMinute) ||
            QualifiesStability(metrics.StabilityGeneration, benchmarks.DecentStabilityGeneration);
        var hasGoodHealing = Qualifies(metrics.HealingPerSecond, benchmarks.GoodHealingPerSecond, MinimumGoodHealingPerSecond);

        if (!hasDecentDamage && hasGoodHealing)
        {
            return WvwHealSupportPlaystyle;
        }

        if (hasDecentSupport)
        {
            return hasDecentDamage ? WvwSupportDpsPlaystyle : WvwSupportPlaystyle;
        }

        return DpsPlaystyle;
    }

    private static WvwMetrics GetWvwMetrics(PlayerFightLog log, long fightDurationInMs)
    {
        var seconds = Math.Max(fightDurationInMs / 1000d, 1d);
        var minutes = seconds / 60d;
        return new WvwMetrics(
            log.Damage / seconds,
            log.Healing / seconds,
            log.Cleanses / minutes,
            log.Strips / minutes,
            (double)(log.StabGenOnGroup + log.StabGenOffGroup));
    }

    private static WvwMetrics GetWvwMetrics(Gw2Player player, long fightDurationInMs)
    {
        var seconds = Math.Max(fightDurationInMs / 1000d, 1d);
        var minutes = seconds / 60d;
        return new WvwMetrics(
            player.Damage / seconds,
            player.Healing / seconds,
            player.Cleanses / minutes,
            player.Strips / minutes,
            player.StabOnGroup + player.StabOffGroup);
    }

    private static long GetDuration(IReadOnlyDictionary<long, long> durationByFightLogId, long fightLogId) =>
        durationByFightLogId.TryGetValue(fightLogId, out var duration) ? duration : 0L;

    private static bool Qualifies(double value, double benchmark, double minimum) =>
        double.IsFinite(value) && value >= Math.Max(benchmark, minimum);

    private static bool QualifiesStability(double value, double benchmark) =>
        double.IsFinite(value) &&
        double.IsFinite(benchmark) &&
        benchmark > 0d &&
        value > 0d &&
        value >= benchmark;

    private static double Percentile(IEnumerable<double> values, double percentile)
    {
        var sortedValues = values
            .Where(v => double.IsFinite(v) && v > 0d)
            .Order()
            .ToList();
        if (sortedValues.Count == 0)
        {
            return 0d;
        }
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

    private sealed record WvwMetrics(
        double DamagePerSecond,
        double HealingPerSecond,
        double CleansesPerMinute,
        double StripsPerMinute,
        double StabilityGeneration);
}

public sealed record WvwPlaystyleBenchmarks(
    double HighDamagePerSecond,
    double DecentDamagePerSecond,
    double DecentHealingPerSecond,
    double GoodHealingPerSecond,
    double DecentCleansesPerMinute,
    double DecentStripsPerMinute,
    double DecentStabilityGeneration);

public sealed record PlaystyleDefinition(string Key, string Label);
