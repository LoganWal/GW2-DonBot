using DonBot.Core.Models.GuildWars2;

namespace DonBot.Core.Services;

public static class PlayerFightLogRoleClassifier
{
    public const string BoonDpsRole = "boon-dps";
    public const string BoonHealerRole = "boon-healer";

    public const double BoonGenerationThreshold = 35d;
    private const double BoonHealerDpsThreshold = 0.33d;

    public static double GetAverageGroupDps(IEnumerable<Gw2Player> players, long fightDurationInMs)
    {
        var playerList = players.ToList();
        if (playerList.Count == 0)
        {
            return 0d;
        }

        var seconds = GetFightSeconds(fightDurationInMs);
        return playerList.Average(player => player.Damage / seconds);
    }

    public static string ResolveBoonRole(Gw2Player player, long fightDurationInMs, double averageGroupDps)
    {
        if (HasBoonGeneration(player) is false)
        {
            return string.Empty;
        }

        if (IsLowDps(player, fightDurationInMs, averageGroupDps))
        {
            return BoonHealerRole;
        }

        return BoonDpsRole;
    }

    public static bool HasBoonGeneration(Gw2Player player) =>
        HasProviderLevelBoonGeneration(player.QuicknessGenGroup) ||
        HasProviderLevelBoonGeneration(player.AlacGenGroup);

    public static bool HasProviderLevelBoonGeneration(double boonGeneration) =>
        boonGeneration > BoonGenerationThreshold;

    public static double AverageProviderLevelBoonGeneration(IEnumerable<decimal> values)
    {
        var providerValues = values
            .Where(value => HasProviderLevelBoonGeneration((double)value))
            .Select(value => (double)value)
            .ToList();
        return providerValues.Count > 0 ? providerValues.Average() : 0d;
    }

    public static bool IsLowDps(Gw2Player player, long fightDurationInMs, double averageGroupDps)
    {
        if (averageGroupDps <= 0d)
        {
            return false;
        }

        return GetPlayerDps(player, fightDurationInMs) < averageGroupDps * BoonHealerDpsThreshold;
    }

    private static double GetPlayerDps(Gw2Player player, long fightDurationInMs) =>
        player.Damage / GetFightSeconds(fightDurationInMs);

    private static double GetFightSeconds(long fightDurationInMs) =>
        Math.Max(fightDurationInMs / 1000d, 1d);
}
