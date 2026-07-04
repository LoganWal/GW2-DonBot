using DonBot.Core.Models.GuildWars2;

namespace DonBot.Core.Services.GuildWars2;

public static class LegacyArcDpsStatsBuilder
{
    public static List<long> BuildOffensiveStats(
        long missed,
        long interrupts,
        long blocked,
        long killed,
        long downed,
        long downContribution)
    {
        var stats = Enumerable.Repeat(0L, ArcDpsDataIndices.OffensiveStatsLength).ToList();
        stats[ArcDpsDataIndices.NumberOfHitsWhileBlindedIndex] = missed;
        stats[ArcDpsDataIndices.InterruptsIndex] = interrupts;
        stats[ArcDpsDataIndices.NumberOfTimesEnemyBlockedAttackIndex] = blocked;
        stats[ArcDpsDataIndices.EnemyDeathIndex] = killed;
        stats[ArcDpsDataIndices.DownIndex] = downed;
        stats[ArcDpsDataIndices.DamageDownContribution] = downContribution;
        return stats;
    }

    public static List<double> BuildGameplayStats(double distanceFromTag)
    {
        var stats = Enumerable.Repeat(0d, ArcDpsDataIndices.GameplayStatsLength).ToList();
        stats[ArcDpsDataIndices.DistanceFromTagIndex] = distanceFromTag;
        return stats;
    }

    public static List<DefStat> BuildDefStats(
        double damageTaken,
        double barrierMitigation,
        double missedCount,
        double interruptedCount,
        double blockedCount,
        double boonStrips,
        double downCount,
        double deadCount)
    {
        var stats = Enumerable.Range(0, ArcDpsDataIndices.DefStatsLength)
            .Select(_ => new DefStat { Double = 0d })
            .ToList();
        stats[ArcDpsDataIndices.DamageTakenIndex] = new DefStat { Double = damageTaken };
        stats[ArcDpsDataIndices.BarrierMitigationIndex] = new DefStat { Double = barrierMitigation };
        stats[ArcDpsDataIndices.NumberOfMissesAgainstIndex] = new DefStat { Double = missedCount };
        stats[ArcDpsDataIndices.TimesInterruptedIndex] = new DefStat { Double = interruptedCount };
        stats[ArcDpsDataIndices.NumberOfTimesBlockedAttackIndex] = new DefStat { Double = blockedCount };
        stats[ArcDpsDataIndices.NumberOfBoonsRippedIndex] = new DefStat { Double = boonStrips };
        stats[ArcDpsDataIndices.EnemiesDownedIndex] = new DefStat { Double = downCount };
        stats[ArcDpsDataIndices.DeathIndex] = new DefStat { Double = deadCount };
        return stats;
    }

    public static List<double> BuildSupportStats(
        double condiCleanseSelf,
        double condiCleanseTimeSelf,
        double condiCleanse,
        double condiCleanseTime,
        double boonStrips)
    {
        var stats = Enumerable.Repeat(0d, ArcDpsDataIndices.SupportStatsLength).ToList();
        stats[ArcDpsDataIndices.SelfCleansesIndex] = condiCleanseSelf;
        stats[ArcDpsDataIndices.SelfCleanseTimeIndex] = condiCleanseTimeSelf;
        stats[ArcDpsDataIndices.PlayerCleansesIndex] = condiCleanse;
        stats[ArcDpsDataIndices.PlayerCleanseTimeIndex] = condiCleanseTime;
        stats[ArcDpsDataIndices.PlayerStripsIndex] = boonStrips;
        return stats;
    }
}
