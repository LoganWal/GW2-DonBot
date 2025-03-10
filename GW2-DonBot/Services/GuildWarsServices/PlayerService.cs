﻿using DonBot.Extensions;
using DonBot.Models.Entities;
using DonBot.Models.Enums;
using DonBot.Models.GuildWars2;
using DonBot.Models.Statics;
using DonBot.Services.DatabaseServices;
using Newtonsoft.Json.Linq;

namespace DonBot.Services.GuildWarsServices
{
    public class PlayerService(IEntityService entityService) : IPlayerService
    {
        public List<Gw2Player> GetGw2Players(EliteInsightDataModel data, ArcDpsPhase fightPhase, HealingPhase healingPhase, BarrierPhase barrierPhase, short? encounterType = null, bool someAllFights = true)
        {
            if (data.Players == null)
            {
                return [];
            }

            var gw2Players = new List<Gw2Player>();
            foreach (var arcDpsPlayer in data.Players)
            {
                if (arcDpsPlayer.Acc == null || arcDpsPlayer.Profession == null || arcDpsPlayer.Name == null || data == null || arcDpsPlayer.NotInSquad)
                {
                    continue;
                }

                var existingPlayer = gw2Players.FirstOrDefault(s => s.AccountName == arcDpsPlayer.Acc);
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
                }
                else
                {
                    existingPlayer.Profession = arcDpsPlayer.Profession;
                    existingPlayer.CharacterName = arcDpsPlayer.Name;
                    existingPlayer.SubGroup = arcDpsPlayer.Group;
                }

                var playerIndex = data.Players.IndexOf(arcDpsPlayer);
                var fightPhaseStats = fightPhase.DpsStatsTargets?.Count >= playerIndex + 1 ? fightPhase.DpsStatsTargets[playerIndex] : null;
                var fightAllDps = fightPhase.DpsStats?.Count >= playerIndex + 1 ? fightPhase.DpsStats[playerIndex] : null;
                var firstFightPhaseStats = (data.Phases?.Count > 2 && fightPhase.DpsStatsTargets?.Count >= playerIndex + 1) ? data.Phases[1].DpsStatsTargets?[playerIndex] : null;
                var supportStats = fightPhase.SupportStats?.Count >= playerIndex + 1 ? fightPhase.SupportStats[playerIndex] : null;
                var boonGenGroupStats = fightPhase.BuffsStatContainer.BoonGenGroupStats?.Count >= playerIndex + 1 ? fightPhase.BuffsStatContainer.BoonGenGroupStats[playerIndex] : null;
                var boonGenOffGroupStats = fightPhase.BuffsStatContainer.BoonGenOGroupStats?.Count >= playerIndex + 1 ? fightPhase.BuffsStatContainer.BoonGenOGroupStats[playerIndex] : null;

                var healingStatsTargets = healingPhase.OutgoingHealingStatsTargets?.Count >= playerIndex + 1 ? healingPhase.OutgoingHealingStatsTargets[playerIndex] : null;
                var barrierStats = barrierPhase.OutgoingBarrierStats?.Count >= playerIndex + 1 ? barrierPhase.OutgoingBarrierStats[playerIndex] : null;
                var gameplayStats = fightPhase.GameplayStats?.Count >= playerIndex + 1 ? fightPhase.GameplayStats[playerIndex] : null;
                var defStats = fightPhase.DefStats?.Count >= playerIndex + 1 ? fightPhase.DefStats[playerIndex] : null;
                var offensiveStatsTargets = fightPhase.OffensiveStatsTargets?.Count >= playerIndex + 1 ? fightPhase.OffensiveStatsTargets[playerIndex] : null;

                var boons = fightPhase.BuffsStatContainer.BoonActiveStats?.Count >= playerIndex + 1 ? fightPhase.BuffsStatContainer.BoonActiveStats[playerIndex].Data : null;
                var mechanics = fightPhase.MechanicStats?.Count >= playerIndex + 1 ? fightPhase.MechanicStats[playerIndex] : null;
                var resurrectionTime = Convert.ToInt32(data.Players[playerIndex].Details?.Rotation?
                    .FirstOrDefault()
                    ?.Where(s => s.Count > ArcDpsDataIndices.RotationSkillIndex && Convert.ToInt32(s[ArcDpsDataIndices.RotationSkillIndex]) == ArcDpsDataIndices.RotationResurrectionSkill)
                    .Sum(s => s[ArcDpsDataIndices.RotationSkillDurationIndex]) * 1000 ?? 0);

                existingPlayer.Kills = offensiveStatsTargets?.Sum(s => s[ArcDpsDataIndices.EnemyDeathIndex]) ?? 0;
                existingPlayer.Deaths = Convert.ToInt64(defStats?[ArcDpsDataIndices.DeathIndex].Double ?? 0L);
                existingPlayer.TimesDowned = defStats?[ArcDpsDataIndices.EnemiesDownedIndex].Double ?? 0;
                existingPlayer.Downs = offensiveStatsTargets?.Sum(s => s[ArcDpsDataIndices.DownIndex]) ?? 0;
                existingPlayer.Damage = (someAllFights ? fightPhaseStats?.Sum(s => s.FirstOrDefault()) : fightPhaseStats?.FirstOrDefault()?.FirstOrDefault()) ?? 0;
                existingPlayer.Cleave = fightAllDps?.FirstOrDefault() - existingPlayer.Damage ?? 0;
                existingPlayer.Cleanses = supportStats?.FirstOrDefault() ?? 0;
                existingPlayer.Cleanses += supportStats?.Count >= ArcDpsDataIndices.PlayerCleansesIndex + 1 ? supportStats[ArcDpsDataIndices.PlayerCleansesIndex] : 0;
                existingPlayer.Strips = supportStats?.Count >= ArcDpsDataIndices.PlayerStripsIndex + 1 ? supportStats[ArcDpsDataIndices.PlayerStripsIndex] : 0;
                existingPlayer.StabOnGroup = boonGenGroupStats?.Data?[ArcDpsDataIndices.BoonStabDimension1Index][0] ?? 0;
                existingPlayer.StabOffGroup = boonGenOffGroupStats?.Data?[ArcDpsDataIndices.BoonStabDimension1Index][0] ?? 0;
                existingPlayer.Healing = healingStatsTargets?.Sum(s => s.FirstOrDefault()) ?? 0;
                existingPlayer.BarrierGenerated = barrierStats?.FirstOrDefault() ?? 0;
                existingPlayer.DistanceFromTag = gameplayStats?[ArcDpsDataIndices.DistanceFromTagIndex] ?? 0;
                existingPlayer.Interrupts = offensiveStatsTargets?.Sum(s => s[ArcDpsDataIndices.InterruptsIndex]) ?? 0;
                existingPlayer.TimesInterrupted = Convert.ToInt64(defStats?[ArcDpsDataIndices.TimesInterruptedIndex].Double ?? 0L);
                existingPlayer.DamageDownContribution = offensiveStatsTargets?.Sum(s => s[ArcDpsDataIndices.DamageDownContribution]) ?? 0;
                existingPlayer.NumberOfHitsWhileBlinded = offensiveStatsTargets?.Sum(s => s[ArcDpsDataIndices.NumberOfHitsWhileBlindedIndex]) ?? 0;
                existingPlayer.NumberOfMissesAgainst = defStats?[ArcDpsDataIndices.NumberOfMissesAgainstIndex].Double ?? 0;
                existingPlayer.NumberOfTimesBlockedAttack = defStats?[ArcDpsDataIndices.NumberOfTimesBlockedAttackIndex].Double ?? 0;
                existingPlayer.NumberOfTimesEnemyBlockedAttack = offensiveStatsTargets?.Sum(s => s[ArcDpsDataIndices.NumberOfTimesEnemyBlockedAttackIndex]) ?? 0;
                existingPlayer.NumberOfBoonsRipped = defStats?[ArcDpsDataIndices.NumberOfBoonsRippedIndex].Double ?? 0;
                existingPlayer.DamageTaken = defStats?[ArcDpsDataIndices.DamageTakenIndex].Double ?? 0;
                existingPlayer.BarrierMitigation = defStats?[ArcDpsDataIndices.BarrierMitigationIndex].Double ?? 0;
                existingPlayer.TotalQuick = boons?.CheckIndexIsValid(ArcDpsDataIndices.TotalQuick, 0) ?? false ? boons[ArcDpsDataIndices.TotalQuick][0] : 0;
                existingPlayer.TotalAlac = boons?.CheckIndexIsValid(ArcDpsDataIndices.TotalAlac, 0) ?? false ? boons[ArcDpsDataIndices.TotalAlac][0] : 0;
                existingPlayer.ResurrectionTime = resurrectionTime;

                switch (encounterType)
                {
                    case (short)FightTypesEnum.ToF:
                    {
                        var possibleMechanics = data.MechanicMap?.Where(s => s.PlayerMech).ToList() ?? [];
                        existingPlayer.CerusOrbsCollected = GetMechanicValueForPlayer(possibleMechanics, "Insatiable Application", mechanics);

                        if (firstFightPhaseStats != null)
                        {
                            existingPlayer.CerusPhaseOneDamage = ((double)firstFightPhaseStats[0][0] / data.Phases?[1].Duration * 1000) ?? 0;
                        }


                        existingPlayer.CerusSpreadHitCount = GetMechanicValueForPlayer(possibleMechanics, "Pool of Despair Hit", mechanics);
                        existingPlayer.CerusSpreadHitCount += GetMechanicValueForPlayer(possibleMechanics, "Empowered Pool of Despair Hit", mechanics);
                        break;
                    }
                    case (short)FightTypesEnum.Deimos:
                    {
                        var possibleMechanics = data.MechanicMap?.Where(s => s.PlayerMech).ToList() ?? [];
                        existingPlayer.DeimosOilsTriggered = GetMechanicValueForPlayer(possibleMechanics, "Black Oil Trigger", mechanics);
                        break;
                    }
                }
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

        public async Task SetPlayerPoints(EliteInsightDataModel eliteInsightDataModel)
        {
            if (eliteInsightDataModel.Players == null)
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

            var fightPhase = eliteInsightDataModel.Phases?.FirstOrDefault() ?? new ArcDpsPhase();
            var healingPhase = eliteInsightDataModel.HealingStatsExtension?.HealingPhases?.FirstOrDefault() ?? new HealingPhase();
            var barrierPhase = eliteInsightDataModel.BarrierStatsExtension?.BarrierPhases?.FirstOrDefault() ?? new BarrierPhase();

            var gw2Players = GetGw2Players(eliteInsightDataModel, fightPhase, healingPhase, barrierPhase);

            var secondsOfFight = 0;
            if (TimeSpan.TryParse(eliteInsightDataModel.EncounterDuration, out var duration))
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
}
