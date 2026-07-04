namespace DonBot.Core.Services;

public sealed record FightLogContentFingerprint(
    short FightType,
    DateTime FightStart,
    IReadOnlySet<string> PlayerAccountNames)
{
    public static FightLogContentFingerprint Create(
        short fightType,
        DateTime fightStart,
        IEnumerable<string> playerAccountNames) =>
        new(
            fightType,
            fightStart,
            playerAccountNames.ToHashSet(StringComparer.OrdinalIgnoreCase));
}
