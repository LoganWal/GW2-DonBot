using DonBot.Core.Models.GuildWars2;
using Newtonsoft.Json.Linq;

namespace DonBot.Core.Services.GuildWars2;

public static class EliteInsightPlayerMapper
{
    public static List<Gw2Player> GetGw2Players(EliteInsightDataModel data, ArcDpsPhase fightPhase, bool sumAllTargets = true)
    {
        if (data.FightEliteInsightDataModel.Players == null)
        {
            return [];
        }

        var gw2Players = new List<Gw2Player>();
        var characterCountPerAccount = new Dictionary<string, int>();

        var stabBoonIndex = data.FightEliteInsightDataModel.Boons.IndexOf(Gw2BoonIds.Stability);
        var quickBoonIndex = data.FightEliteInsightDataModel.Boons.IndexOf(Gw2BoonIds.Quickness);
        var alacBoonIndex = data.FightEliteInsightDataModel.Boons.IndexOf(Gw2BoonIds.Alacrity);
        var fightPhaseIndex = ResolveFightPhaseIndex(data, fightPhase);
        var healingPhase = ResolveExtensionPhase(data.HealingEliteInsightDataModel.HealingPhases, fightPhaseIndex);
        var barrierPhase = ResolveExtensionPhase(data.BarrierEliteInsightDataModel.BarrierPhases, fightPhaseIndex);

        foreach (var arcDpsPlayer in data.FightEliteInsightDataModel.Players)
        {
            if (arcDpsPlayer.Acc == null || arcDpsPlayer.Profession == null || arcDpsPlayer.Name == null || arcDpsPlayer.NotInSquad)
            {
                continue;
            }

            var existingPlayer = gw2Players.FirstOrDefault(s => s.AccountName == arcDpsPlayer.Acc);
            var isNewPlayer = false;
            if (existingPlayer == null)
            {
                existingPlayer = new Gw2Player
                {
                    AccountName = arcDpsPlayer.Acc,
                    Profession = arcDpsPlayer.Profession,
                    CharacterName = arcDpsPlayer.Name,
                    SubGroup = arcDpsPlayer.Group,
                };
                gw2Players.Add(existingPlayer);
                characterCountPerAccount[arcDpsPlayer.Acc] = 1;
                isNewPlayer = true;
            }
            else
            {
                existingPlayer.Profession = arcDpsPlayer.Profession;
                existingPlayer.CharacterName = arcDpsPlayer.Name;
                existingPlayer.SubGroup = arcDpsPlayer.Group;
                characterCountPerAccount[arcDpsPlayer.Acc]++;
            }

            var playerIndex = data.FightEliteInsightDataModel.Players.IndexOf(arcDpsPlayer);
            var fightPhaseStats = fightPhase.DpsStatsTargets?.Count >= playerIndex + 1 ? fightPhase.DpsStatsTargets[playerIndex] : null;
            var fightAllDps = fightPhase.DpsStats?.Count >= playerIndex + 1 ? fightPhase.DpsStats[playerIndex] : null;
            var supportStats = fightPhase.SupportStats?.Count >= playerIndex + 1 ? fightPhase.SupportStats[playerIndex] : null;
            var boonGenGroupStats = fightPhase.BuffsStatContainer.BoonGenGroupStats?.Count >= playerIndex + 1 ? fightPhase.BuffsStatContainer.BoonGenGroupStats[playerIndex] : null;
            var boonGenOffGroupStats = fightPhase.BuffsStatContainer.BoonGenOGroupStats?.Count >= playerIndex + 1 ? fightPhase.BuffsStatContainer.BoonGenOGroupStats[playerIndex] : null;

            var healingStatsTargets = healingPhase != null && healingPhase.OutgoingHealingStatsTargets.Count >= playerIndex + 1 ? healingPhase.OutgoingHealingStatsTargets[playerIndex] : null;
            var barrierStats = barrierPhase != null && barrierPhase.OutgoingBarrierStats.Count >= playerIndex + 1 ? barrierPhase.OutgoingBarrierStats[playerIndex] : null;
            var gameplayStats = fightPhase.GameplayStats?.Count >= playerIndex + 1 ? fightPhase.GameplayStats[playerIndex] : null;
            var defStats = fightPhase.DefStats?.Count >= playerIndex + 1 ? fightPhase.DefStats[playerIndex] : null;
            var offensiveStatsTargets = fightPhase.OffensiveStatsTargets?.Count >= playerIndex + 1 ? fightPhase.OffensiveStatsTargets[playerIndex] : null;
            var playerDetails = data.FightEliteInsightDataModel.Players[playerIndex].Details;

            var boons = fightPhase.BuffsStatContainer.BoonActiveStats?.Count >= playerIndex + 1 ? fightPhase.BuffsStatContainer.BoonActiveStats[playerIndex].Data : null;
            var mechanics = fightPhase.MechanicStats?.Count >= playerIndex + 1 ? fightPhase.MechanicStats[playerIndex] : null;

            long? deathTime = null;
            if (playerDetails?.DeathRecap != null)
            {
                deathTime = playerDetails.DeathRecap.FirstOrDefault()?.Time;
            }
            var resurrectionTime = Convert.ToInt32(playerDetails?.Rotation?
                .FirstOrDefault()
                ?.Where(s => s.Count > ArcDpsDataIndices.RotationSkillIndex && Convert.ToInt32(s[ArcDpsDataIndices.RotationSkillIndex]) == Gw2SkillIds.Resurrection)
                .Sum(s => s[ArcDpsDataIndices.RotationSkillDurationIndex]) ?? 0);

            try
            {
                var kills = offensiveStatsTargets?.Sum(s => s[ArcDpsDataIndices.EnemyDeathIndex]) ?? 0;
                var deaths = Convert.ToInt64(defStats?[ArcDpsDataIndices.DeathIndex].Double ?? 0L);
                var timesDowned = defStats?[ArcDpsDataIndices.EnemiesDownedIndex].Double ?? 0;
                var downs = offensiveStatsTargets?.Sum(s => s[ArcDpsDataIndices.DownIndex]) ?? 0;
                var damage = (sumAllTargets ? fightPhaseStats?.Sum(s => s.FirstOrDefault()) : fightPhaseStats?.FirstOrDefault()?.FirstOrDefault()) ?? 0;
                var cleave = fightAllDps?.FirstOrDefault() - damage ?? 0;
                var cleanses = supportStats?.FirstOrDefault() ?? 0;
                cleanses += supportStats?.Count >= ArcDpsDataIndices.PlayerCleansesIndex + 1 ? supportStats[ArcDpsDataIndices.PlayerCleansesIndex] : 0;
                var strips = supportStats?.Count >= ArcDpsDataIndices.PlayerStripsIndex + 1 ? supportStats[ArcDpsDataIndices.PlayerStripsIndex] : 0;
                var stabOnGroup = GetBoonValue(boonGenGroupStats, stabBoonIndex);
                var stabOffGroup = GetBoonValue(boonGenOffGroupStats, stabBoonIndex);
                var quicknessGenGroup = GetBoonValue(boonGenGroupStats, quickBoonIndex);
                var alacGenGroup = GetBoonValue(boonGenGroupStats, alacBoonIndex);
                var healing = healingStatsTargets?.Sum(s => s.FirstOrDefault()) ?? 0;
                var barrierGenerated = barrierStats?.FirstOrDefault() ?? 0;
                var distanceFromTag = gameplayStats?[ArcDpsDataIndices.DistanceFromTagIndex] ?? 0;
                var interrupts = offensiveStatsTargets?.Sum(s => s[ArcDpsDataIndices.InterruptsIndex]) ?? 0;
                var timesInterrupted = Convert.ToInt64(defStats?[ArcDpsDataIndices.TimesInterruptedIndex].Double ?? 0L);
                var damageDownContribution = offensiveStatsTargets?.Sum(s => s[ArcDpsDataIndices.DamageDownContribution]) ?? 0;
                var numberOfHitsWhileBlinded = offensiveStatsTargets?.Sum(s => s[ArcDpsDataIndices.NumberOfHitsWhileBlindedIndex]) ?? 0;
                var numberOfMissesAgainst = defStats?[ArcDpsDataIndices.NumberOfMissesAgainstIndex].Double ?? 0;
                var numberOfTimesBlockedAttack = defStats?[ArcDpsDataIndices.NumberOfTimesBlockedAttackIndex].Double ?? 0;
                var numberOfTimesEnemyBlockedAttack = offensiveStatsTargets?.Sum(s => s[ArcDpsDataIndices.NumberOfTimesEnemyBlockedAttackIndex]) ?? 0;
                var numberOfBoonsRipped = defStats?[ArcDpsDataIndices.NumberOfBoonsRippedIndex].Double ?? 0;
                var damageTaken = defStats?[ArcDpsDataIndices.DamageTakenIndex].Double ?? 0;
                var barrierMitigation = defStats?[ArcDpsDataIndices.BarrierMitigationIndex].Double ?? 0;
                var totalQuick = GetBoonUptime(boons, quickBoonIndex);
                var totalAlac = GetBoonUptime(boons, alacBoonIndex);

                if (isNewPlayer)
                {
                    existingPlayer.Kills = kills;
                    existingPlayer.Deaths = deaths;
                    existingPlayer.TimesDowned = timesDowned;
                    existingPlayer.Downs = downs;
                    existingPlayer.Damage = damage;
                    existingPlayer.Cleave = cleave;
                    existingPlayer.Cleanses = cleanses;
                    existingPlayer.Strips = strips;
                    existingPlayer.StabOnGroup = stabOnGroup;
                    existingPlayer.StabOffGroup = stabOffGroup;
                    existingPlayer.QuicknessGenGroup = quicknessGenGroup;
                    existingPlayer.AlacGenGroup = alacGenGroup;
                    existingPlayer.Healing = healing;
                    existingPlayer.BarrierGenerated = barrierGenerated;
                    existingPlayer.DistanceFromTag = distanceFromTag;
                    existingPlayer.Interrupts = interrupts;
                    existingPlayer.TimesInterrupted = timesInterrupted;
                    existingPlayer.DamageDownContribution = damageDownContribution;
                    existingPlayer.NumberOfHitsWhileBlinded = numberOfHitsWhileBlinded;
                    existingPlayer.NumberOfMissesAgainst = numberOfMissesAgainst;
                    existingPlayer.NumberOfTimesBlockedAttack = numberOfTimesBlockedAttack;
                    existingPlayer.NumberOfTimesEnemyBlockedAttack = numberOfTimesEnemyBlockedAttack;
                    existingPlayer.NumberOfBoonsRipped = numberOfBoonsRipped;
                    existingPlayer.DamageTaken = damageTaken;
                    existingPlayer.BarrierMitigation = barrierMitigation;
                    existingPlayer.TotalQuick = totalQuick;
                    existingPlayer.TotalAlac = totalAlac;
                    existingPlayer.ResurrectionTime = resurrectionTime;
                    existingPlayer.TimeOfDeath = deathTime;
                }
                else
                {
                    existingPlayer.Kills += kills;
                    existingPlayer.Deaths += deaths;
                    existingPlayer.TimesDowned += timesDowned;
                    existingPlayer.Downs += downs;
                    existingPlayer.Damage += damage;
                    existingPlayer.Cleave += cleave;
                    existingPlayer.Cleanses += cleanses;
                    existingPlayer.Strips += strips;
                    existingPlayer.StabOnGroup += stabOnGroup;
                    existingPlayer.StabOffGroup += stabOffGroup;
                    existingPlayer.QuicknessGenGroup += quicknessGenGroup;
                    existingPlayer.AlacGenGroup += alacGenGroup;
                    existingPlayer.Healing += healing;
                    existingPlayer.BarrierGenerated += barrierGenerated;
                    existingPlayer.DistanceFromTag += distanceFromTag;
                    existingPlayer.Interrupts += interrupts;
                    existingPlayer.TimesInterrupted += timesInterrupted;
                    existingPlayer.DamageDownContribution += damageDownContribution;
                    existingPlayer.NumberOfHitsWhileBlinded += numberOfHitsWhileBlinded;
                    existingPlayer.NumberOfMissesAgainst += numberOfMissesAgainst;
                    existingPlayer.NumberOfTimesBlockedAttack += numberOfTimesBlockedAttack;
                    existingPlayer.NumberOfTimesEnemyBlockedAttack += numberOfTimesEnemyBlockedAttack;
                    existingPlayer.NumberOfBoonsRipped += numberOfBoonsRipped;
                    existingPlayer.DamageTaken += damageTaken;
                    existingPlayer.BarrierMitigation += barrierMitigation;
                    existingPlayer.TotalQuick += totalQuick;
                    existingPlayer.TotalAlac += totalAlac;
                    existingPlayer.ResurrectionTime += resurrectionTime;
                    if (deathTime.HasValue && (!existingPlayer.TimeOfDeath.HasValue || deathTime.Value < existingPlayer.TimeOfDeath.Value))
                    {
                        existingPlayer.TimeOfDeath = deathTime;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Player: {arcDpsPlayer.Acc}");
                Console.WriteLine($"Log: {data.FightEliteInsightDataModel.Url}");
                Console.WriteLine($"Error parsing player data: {ex.Message}");
                Console.WriteLine($"Stack: {ex.StackTrace}");
            }

            var possibleMechanics = data.FightEliteInsightDataModel.MechanicMap?.Where(s => s.PlayerMech).ToList() ?? [];
            for (var mechIndex = 0; mechIndex < possibleMechanics.Count; mechIndex++)
            {
                var mechanic = possibleMechanics[mechIndex];
                if (string.IsNullOrEmpty(mechanic.Name) || mechanics == null || mechIndex >= mechanics.Count)
                {
                    continue;
                }
                var value = (mechanics[mechIndex] as JArray)?.Select(s => (long)s).FirstOrDefault() ?? 0;
                if (value <= 0)
                {
                    continue;
                }
                existingPlayer.Mechanics.TryGetValue(mechanic.Name, out var existing);
                existingPlayer.Mechanics[mechanic.Name] = existing + value;
            }
        }

        foreach (var player in gw2Players)
        {
            if (!characterCountPerAccount.TryGetValue(player.AccountName, out var count) || count <= 1)
            {
                continue;
            }

            player.StabOnGroup /= count;
            player.StabOffGroup /= count;
            player.QuicknessGenGroup /= count;
            player.AlacGenGroup /= count;
            player.DistanceFromTag /= count;
            player.TotalQuick /= count;
            player.TotalAlac /= count;
        }

        return gw2Players;
    }

    private static double GetBoonValue(BoonActiveStat? stats, int boonIndex)
    {
        if (boonIndex < 0 || !HasNestedIndex(stats?.Data, boonIndex, 0))
        {
            return 0d;
        }

        return stats!.Data![boonIndex][0];
    }

    private static double GetBoonUptime(List<List<double>>? boons, int boonIndex) =>
        HasNestedIndex(boons, boonIndex, 0) ? boons![boonIndex][0] : 0d;

    private static bool HasNestedIndex<T>(List<List<T>>? source, int outerIndex, int innerIndex) =>
        source != null &&
        outerIndex >= 0 &&
        outerIndex < source.Count &&
        innerIndex >= 0 &&
        innerIndex < source[outerIndex].Count;

    private static int ResolveFightPhaseIndex(EliteInsightDataModel data, ArcDpsPhase fightPhase)
    {
        var phaseIndex = data.FightEliteInsightDataModel.Phases?.IndexOf(fightPhase) ?? 0;
        return Math.Max(phaseIndex, 0);
    }

    private static TPhase? ResolveExtensionPhase<TPhase>(List<TPhase> phases, int fightPhaseIndex) where TPhase : class
    {
        if (phases.Count == 0)
        {
            return null;
        }

        return fightPhaseIndex < phases.Count ? phases[fightPhaseIndex] : phases[0];
    }
}
