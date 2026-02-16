using DonBot.Extensions;
using DonBot.Models.Entities;
using DonBot.Models.Enums;
using DonBot.Models.GuildWars2;
using DonBot.Models.Statics;
using DonBot.Services.DatabaseServices;
using Newtonsoft.Json.Linq;

namespace DonBot.Services.GuildWarsServices;

public sealed class PlayerService(IEntityService entityService) : IPlayerService
{
    public List<Gw2Player> GetGw2Players(EliteInsightDataModel data, ArcDpsPhase fightPhase, short? encounterType = null, bool someAllFights = true)
    {
        if (data.FightEliteInsightDataModel.Players == null)
        {
            return [];
        }

        var gw2Players = new List<Gw2Player>();
        var characterCountPerAccount = new Dictionary<string, int>();
        
        foreach (var arcDpsPlayer in data.FightEliteInsightDataModel.Players)
        {
            if (arcDpsPlayer.Acc == null || arcDpsPlayer.Profession == null || arcDpsPlayer.Name == null || data == null || arcDpsPlayer.NotInSquad)
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
            var firstFightPhaseStats = (data.FightEliteInsightDataModel.Phases?.Count > 2 && fightPhase.DpsStatsTargets?.Count >= playerIndex + 1) ? data.FightEliteInsightDataModel.Phases[1].DpsStatsTargets?[playerIndex] : null;
            var supportStats = fightPhase.SupportStats?.Count >= playerIndex + 1 ? fightPhase.SupportStats[playerIndex] : null;
            var boonGenGroupStats = fightPhase.BuffsStatContainer.BoonGenGroupStats?.Count >= playerIndex + 1 ? fightPhase.BuffsStatContainer.BoonGenGroupStats[playerIndex] : null;
            var boonGenOffGroupStats = fightPhase.BuffsStatContainer.BoonGenOGroupStats?.Count >= playerIndex + 1 ? fightPhase.BuffsStatContainer.BoonGenOGroupStats[playerIndex] : null;

            var healingStatsTargets = data.HealingEliteInsightDataModel.HealingPhases.Any() ? (data.HealingEliteInsightDataModel.HealingPhases[0].OutgoingHealingStatsTargets.Count >= playerIndex + 1 ? data.HealingEliteInsightDataModel.HealingPhases[0].OutgoingHealingStatsTargets[playerIndex] : null) : null;
            var barrierStats = data.BarrierEliteInsightDataModel.BarrierPhases.Any() ? (data.BarrierEliteInsightDataModel.BarrierPhases[0].OutgoingBarrierStats.Count >= playerIndex + 1 ? data.BarrierEliteInsightDataModel.BarrierPhases[0].OutgoingBarrierStats[playerIndex] : null) : null;
            var gameplayStats = fightPhase.GameplayStats?.Count >= playerIndex + 1 ? fightPhase.GameplayStats[playerIndex] : null;
            var defStats = fightPhase.DefStats?.Count >= playerIndex + 1 ? fightPhase.DefStats[playerIndex] : null;
            var offensiveStatsTargets = fightPhase.OffensiveStatsTargets?.Count >= playerIndex + 1 ? fightPhase.OffensiveStatsTargets[playerIndex] : null;
            var playerDetails = data.FightEliteInsightDataModel.Players[playerIndex].Details;
            
            var boons = fightPhase.BuffsStatContainer.BoonActiveStats?.Count >= playerIndex + 1 ? fightPhase.BuffsStatContainer.BoonActiveStats[playerIndex].Data : null;
            var mechanics = fightPhase.MechanicStats?.Count >= playerIndex + 1 ? fightPhase.MechanicStats[playerIndex] : null;
            var deathTime = playerDetails?.DeathRecap?.FirstOrDefault()?.Time;
            var resurrectionTime = Convert.ToInt32(playerDetails?.Rotation?
                .FirstOrDefault()
                ?.Where(s => s.Count > ArcDpsDataIndices.RotationSkillIndex && Convert.ToInt32(s[ArcDpsDataIndices.RotationSkillIndex]) == ArcDpsDataIndices.RotationResurrectionSkill)
                .Sum(s => s[ArcDpsDataIndices.RotationSkillDurationIndex]) ?? 0);

            try
            {
                var kills = offensiveStatsTargets?.Sum(s => s[ArcDpsDataIndices.EnemyDeathIndex]) ?? 0;
                var deaths = Convert.ToInt64(defStats?[ArcDpsDataIndices.DeathIndex].Double ?? 0L);
                var timesDowned = defStats?[ArcDpsDataIndices.EnemiesDownedIndex].Double ?? 0;
                var downs = offensiveStatsTargets?.Sum(s => s[ArcDpsDataIndices.DownIndex]) ?? 0;
                var damage = (someAllFights ? fightPhaseStats?.Sum(s => s.FirstOrDefault()) : fightPhaseStats?.FirstOrDefault()?.FirstOrDefault()) ?? 0;
                var cleave = fightAllDps?.FirstOrDefault() - damage ?? 0;
                var cleanses = supportStats?.FirstOrDefault() ?? 0;
                cleanses += supportStats?.Count >= ArcDpsDataIndices.PlayerCleansesIndex + 1 ? supportStats[ArcDpsDataIndices.PlayerCleansesIndex] : 0;
                var strips = supportStats?.Count >= ArcDpsDataIndices.PlayerStripsIndex + 1 ? supportStats[ArcDpsDataIndices.PlayerStripsIndex] : 0;
                var stabOnGroup = boonGenGroupStats?.Data?.Count >= ArcDpsDataIndices.BoonStabDimension1Index - 1 ? boonGenGroupStats.Data?[ArcDpsDataIndices.BoonStabDimension1Index][0] ?? 0 : 0;
                var stabOffGroup = boonGenOffGroupStats?.Data?[ArcDpsDataIndices.BoonStabDimension1Index][0] ?? 0;
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
                var totalQuick = boons?.CheckIndexIsValid(ArcDpsDataIndices.TotalQuick, 0) ?? false ? boons[ArcDpsDataIndices.TotalQuick][0] : 0;
                var totalAlac = boons?.CheckIndexIsValid(ArcDpsDataIndices.TotalAlac, 0) ?? false ? boons[ArcDpsDataIndices.TotalAlac][0] : 0;

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
                    existingPlayer.TimeOfDeath = deathTime != null && existingPlayer.TimeOfDeath >= deathTime ? deathTime : existingPlayer.TimeOfDeath;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Player: {arcDpsPlayer.Acc}");
                Console.WriteLine($"Log: {data.FightEliteInsightDataModel.Url}");
                Console.WriteLine($"Error parsing player data: {ex.Message}");
                Console.WriteLine($"Stack: {ex.StackTrace}");
            }

            switch (encounterType)
            {
                case (short)FightTypesEnum.ToF:
                {
                    var possibleMechanics = data.FightEliteInsightDataModel.MechanicMap?.Where(s => s.PlayerMech).ToList() ?? [];
                    var cerusOrbsCollected = GetMechanicValueForPlayer(possibleMechanics, "Insatiable Application", mechanics);
                    var cerusSpreadHitCount = GetMechanicValueForPlayer(possibleMechanics, "Pool of Despair Hit", mechanics);
                    cerusSpreadHitCount += GetMechanicValueForPlayer(possibleMechanics, "Empowered Pool of Despair Hit", mechanics);

                    if (isNewPlayer)
                    {
                        existingPlayer.CerusOrbsCollected = cerusOrbsCollected;
                        existingPlayer.CerusSpreadHitCount = cerusSpreadHitCount;
                    }
                    else
                    {
                        existingPlayer.CerusOrbsCollected += cerusOrbsCollected;
                        existingPlayer.CerusSpreadHitCount += cerusSpreadHitCount;
                    }

                    if (firstFightPhaseStats != null)
                    {
                        var phaseOneDamage = ((double)firstFightPhaseStats[0][0] / data.FightEliteInsightDataModel.Phases?[1].Duration * 1000) ?? 0;
                        if (isNewPlayer)
                        {
                            existingPlayer.CerusPhaseOneDamage = phaseOneDamage;
                        }
                        else
                        {
                            existingPlayer.CerusPhaseOneDamage += phaseOneDamage;
                        }
                    }

                    break;
                }
                case (short)FightTypesEnum.Deimos:
                {
                    var possibleMechanics = data.FightEliteInsightDataModel.MechanicMap?.Where(s => s.PlayerMech).ToList() ?? [];
                    var deimosOilsTriggered = GetMechanicValueForPlayer(possibleMechanics, "Black Oil Trigger", mechanics);
                    
                    if (isNewPlayer)
                    {
                        existingPlayer.DeimosOilsTriggered = deimosOilsTriggered;
                    }
                    else
                    {
                        existingPlayer.DeimosOilsTriggered += deimosOilsTriggered;
                    }
                    break;
                }
                case (short)FightTypesEnum.Ura:
                {
                    var possibleMechanics = data.FightEliteInsightDataModel.MechanicMap?.Where(s => s.PlayerMech).ToList() ?? [];
                    var shardPickUp = GetMechanicValueForPlayer(possibleMechanics, "Bloodstone Shard Pick-up", mechanics);
                    var shardUsed = GetMechanicValueForPlayer(possibleMechanics, "Used Dispel", mechanics);
                    
                    if (isNewPlayer)
                    {
                        existingPlayer.ShardPickUp = shardPickUp;
                        existingPlayer.ShardUsed = shardUsed;
                    }
                    else
                    {
                        existingPlayer.ShardPickUp += shardPickUp;
                        existingPlayer.ShardUsed += shardUsed;
                    }
                    break;
                }
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
            player.DistanceFromTag /= count;
            player.TotalQuick /= count;
            player.TotalAlac /= count;
        }

        return gw2Players;
    }

    private static long GetMechanicValueForPlayer(List<MechanicMap> data, string mechanicName, List<object>? mechanics)
    {
        var oilMechanicIndex = data.FindIndex(s => string.Equals(s.Name, mechanicName, StringComparison.Ordinal));
        if (oilMechanicIndex != -1)
        {
            return (mechanics?[oilMechanicIndex] as JArray)?.Select(s => (long)s).FirstOrDefault() ?? 0;
        }

        return 0;
    }

    public async Task SetPlayerPoints(EliteInsightDataModel fightEliteInsightDataModel)
    {
        if (fightEliteInsightDataModel.FightEliteInsightDataModel.Players == null)
        {
            return;
        }

        var accounts = await entityService.Account.GetAllAsync();
        var gw2Accounts = await entityService.GuildWarsAccount.GetAllAsync();
        accounts = accounts.Where(s => gw2Accounts.Any(acc => acc.DiscordId == s.DiscordId)).ToList();
        if (!accounts.Any())
        {
            return;
        }

        var fightPhase = fightEliteInsightDataModel.FightEliteInsightDataModel.Phases?.FirstOrDefault() ?? new ArcDpsPhase();

        var gw2Players = GetGw2Players(fightEliteInsightDataModel, fightPhase);

        var secondsOfFight = 0;
        if (TimeSpan.TryParse(fightEliteInsightDataModel.FightEliteInsightDataModel.Phases?.FirstOrDefault()?.EncounterDuration, out var duration))
        {
            secondsOfFight = (int)duration.TotalSeconds;
        }
                    
        var currentDateTimeUtc = DateTime.UtcNow;
        foreach (var account in accounts)
        {
            account.PreviousPoints = account.Points;
        }

        await entityService.Account.UpdateRangeAsync(accounts);

        var accountsToUpdate = new List<Account>();

        foreach (var player in gw2Players)
        {
            var gw2Account = gw2Accounts.FirstOrDefault(a => string.Equals(a.GuildWarsAccountName, player.AccountName, StringComparison.OrdinalIgnoreCase));
            if (gw2Account == null)
            {
                continue;
            }

            var account = await entityService.Account.GetFirstOrDefaultAsync(s => s.DiscordId == gw2Account.DiscordId);
            if (account == null)
            {
                continue;
            }

            var totalPoints = 0d;

            // Calculate the total points based on player data
            totalPoints += Math.Min(Convert.ToDouble(player.Damage) / 50000d, 10);
            totalPoints += Math.Min(Convert.ToDouble(player.Cleanses) / 100d, 5);
            totalPoints += Math.Min(Convert.ToDouble(player.Strips) / 30d, 3);
            totalPoints += Math.Min(Convert.ToDouble(player.StabOnGroup) * (secondsOfFight < 20d ? 1d : secondsOfFight / 20d), 6);
            totalPoints += Math.Min(Convert.ToDouble(player.Healing) / 50000d, 4);
            totalPoints += Math.Min(Convert.ToDouble(player.BarrierGenerated) / 40000d, 3);

            totalPoints = Math.Max(4, Math.Min(12, totalPoints));

            // Update the account's points
            account.Points += Convert.ToDecimal(totalPoints);
            account.AvailablePoints += Convert.ToDecimal(totalPoints);
            account.LastWvwLogDateTime = currentDateTimeUtc;

            // Add the updated account to the list
            accountsToUpdate.Add(account);
        }

        // Update all the accounts in bulk
        if (accountsToUpdate.Any())
        {
            await entityService.Account.UpdateRangeAsync(accountsToUpdate);
        }
    }
}