using DonBot.Core.Models.Entities;
using DonBot.Core.Models.GuildWars2;
using DonBot.Core.Services;

namespace DonBot.Core.Services.GuildWars2;

public static class PlayerFightLogFactory
{
    public static List<PlayerFightLog> CreatePve(IEnumerable<Gw2Player> players, long fightLogId, long fightDurationInMs)
    {
        var playerList = players.ToList();
        var averageGroupDps = PlayerFightLogRoleClassifier.GetAverageGroupDps(playerList, fightDurationInMs);
        return playerList
            .Select(player => Create(player, fightLogId, fightDurationInMs, averageGroupDps, isWvW: false))
            .ToList();
    }

    public static List<PlayerFightLog> CreateWvw(
        IEnumerable<Gw2Player> players,
        long fightLogId,
        long fightDurationInMs,
        WvwPlaystyleBenchmarks? wvwBenchmarks = null)
    {
        var playerList = players.ToList();
        var averageGroupDps = PlayerFightLogRoleClassifier.GetAverageGroupDps(playerList, fightDurationInMs);
        var benchmarks = wvwBenchmarks ?? PlayerFightLogPlaystyleClassifier.BuildWvwBenchmarks(playerList, fightDurationInMs);
        return playerList
            .Select(player => Create(player, fightLogId, fightDurationInMs, averageGroupDps, isWvW: true, benchmarks))
            .ToList();
    }

    private static PlayerFightLog Create(
        Gw2Player player,
        long fightLogId,
        long fightDurationInMs,
        double averageGroupDps,
        bool isWvW,
        WvwPlaystyleBenchmarks? wvwBenchmarks = null)
    {
        var boonRole = PlayerFightLogRoleClassifier.ResolveBoonRole(player, fightDurationInMs, averageGroupDps);
        return new PlayerFightLog
        {
            FightLogId = fightLogId,
            GuildWarsAccountName = player.AccountName,
            CharacterName = player.CharacterName,
            Damage = player.Damage,
            Cleave = player.Cleave,
            Kills = player.Kills,
            Downs = player.Downs,
            Deaths = player.Deaths,
            QuicknessDuration = Decimal(player.TotalQuick),
            AlacDuration = Decimal(player.TotalAlac),
            QuicknessGenGroup = Decimal(player.QuicknessGenGroup),
            AlacGenGroup = Decimal(player.AlacGenGroup),
            BoonRole = boonRole,
            Playstyle = isWvW
                ? PlayerFightLogPlaystyleClassifier.ResolveWvwPlaystyle(player, fightDurationInMs, wvwBenchmarks!)
                : PlayerFightLogPlaystyleClassifier.ResolvePvePlaystyle(player, fightDurationInMs, averageGroupDps),
            SubGroup = player.SubGroup,
            DamageDownContribution = player.DamageDownContribution,
            Cleanses = Convert.ToInt64(player.Cleanses),
            Strips = Convert.ToInt64(player.Strips),
            StabGenOnGroup = Decimal(player.StabOnGroup),
            StabGenOffGroup = Decimal(player.StabOffGroup),
            Healing = player.Healing,
            BarrierGenerated = player.BarrierGenerated,
            DistanceFromTag = Decimal(player.DistanceFromTag),
            TimesDowned = Convert.ToInt32(player.TimesDowned),
            Interrupts = player.Interrupts,
            NumberOfHitsWhileBlinded = player.NumberOfHitsWhileBlinded,
            NumberOfMissesAgainst = Convert.ToInt64(player.NumberOfMissesAgainst),
            NumberOfTimesBlockedAttack = Convert.ToInt64(player.NumberOfTimesBlockedAttack),
            NumberOfTimesEnemyBlockedAttack = player.NumberOfTimesEnemyBlockedAttack,
            NumberOfBoonsRipped = Convert.ToInt64(player.NumberOfBoonsRipped),
            DamageTaken = Convert.ToInt64(player.DamageTaken),
            BarrierMitigation = Convert.ToInt64(player.BarrierMitigation),
            TimesInterrupted = player.TimesInterrupted,
            ResurrectionTime = player.ResurrectionTime,
            TimeOfDeath = player.TimeOfDeath
        };
    }

    private static decimal Decimal(double value, int decimals = 2) =>
        double.IsFinite(value) ? Math.Round((decimal)value, decimals) : 0m;
}
