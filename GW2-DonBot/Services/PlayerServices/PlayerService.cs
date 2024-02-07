using Extensions;
using Microsoft.EntityFrameworkCore;
using Models;
using Models.Entities;
using Models.Statics;
using Services.PlayerServices;

namespace Services.Logging
{
    public class PlayerService : IPlayerService
    {
        private readonly DatabaseContext _databaseContext;

        public PlayerService(DatabaseContext databaseContext)
        {
            _databaseContext = databaseContext;
        }

        public List<Gw2Player> GetGw2Players(EliteInsightDataModel data, ArcDpsPhase fightPhase, HealingPhase healingPhase, BarrierPhase barrierPhase)
        {
            if (data.Players == null)
            {
                return new List<Gw2Player>();
            }

            var gw2Players = new List<Gw2Player>();
            foreach (var arcDpsPlayer in data.Players)
            {
                if (arcDpsPlayer == null || arcDpsPlayer.Acc == null || arcDpsPlayer.Profession == null || arcDpsPlayer.Name == null)
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
                var supportStats = fightPhase.SupportStats?.Count >= playerIndex + 1 ? fightPhase.SupportStats[playerIndex] : null;
                var boonGenSquadStats = fightPhase.BoonGenSquadStats?.Count >= playerIndex + 1 ? fightPhase.BoonGenSquadStats[playerIndex] : null;
                var healingStatsTargets = healingPhase.OutgoingHealingStatsTargets?.Count >= playerIndex + 1 ? healingPhase.OutgoingHealingStatsTargets[playerIndex] : null;
                var barrierStats = barrierPhase.OutgoingBarrierStats?.Count >= playerIndex + 1 ? barrierPhase.OutgoingBarrierStats[playerIndex] : null;
                var gameplayStats = fightPhase.GameplayStats?.Count >= playerIndex + 1 ? fightPhase.GameplayStats[playerIndex] : null;
                var defStats = fightPhase.DefStats?.Count >= playerIndex + 1 ? fightPhase.DefStats[playerIndex] : null;
                var offensiveStats = fightPhase.OffensiveStats?.Count >= playerIndex + 1 ? fightPhase.OffensiveStats[playerIndex] : null;

                existingPlayer.Damage = fightPhaseStats?.Sum(s => s.FirstOrDefault()) ?? 0;
                existingPlayer.Cleanses = supportStats?.FirstOrDefault() ?? 0;
                existingPlayer.Cleanses += supportStats?.Count >= ArcDpsDataIndices.PlayerCleansesIndex + 1 ? supportStats[ArcDpsDataIndices.PlayerCleansesIndex] : 0;
                existingPlayer.Strips = supportStats?.Count >= ArcDpsDataIndices.PlayerStripsIndex + 1 ? supportStats[ArcDpsDataIndices.PlayerStripsIndex] : 0;
                existingPlayer.StabUpTime = boonGenSquadStats?.Data?.CheckIndexIsValid(ArcDpsDataIndices.BoonStabDimension1Index, ArcDpsDataIndices.BoonStabDimension2Index) ?? false ? boonGenSquadStats.Data[ArcDpsDataIndices.BoonStabDimension1Index][ArcDpsDataIndices.BoonStabDimension2Index] : 0;
                existingPlayer.Healing = healingStatsTargets?.Sum(s => s.FirstOrDefault()) ?? 0;
                existingPlayer.Barrier = barrierStats?.FirstOrDefault() ?? 0;
                existingPlayer.DistanceFromTag = gameplayStats?[ArcDpsDataIndices.DistanceFromTagIndex] ?? 0;
                existingPlayer.TimesDowned = defStats?[ArcDpsDataIndices.FriendlyDownIndex].Double ?? 0;
                existingPlayer.Interrupts = offensiveStats?[ArcDpsDataIndices.InterruptsIndex] ?? 0;
            }

            return gw2Players;
        }

        public async Task SetPlayerPoints(EliteInsightDataModel eliteInsightDataModel)
        {
            if (eliteInsightDataModel.Players == null)
            {
                return;
            }

            var accounts = await _databaseContext.Account.Where(acc => acc.Gw2ApiKey != null).ToListAsync();
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

            _databaseContext.UpdateRange(accounts);
            await _databaseContext.SaveChangesAsync();

            foreach (var player in gw2Players)
            {
                var account = accounts.FirstOrDefault(a => a.Gw2AccountName == player.AccountName);
                if (account == null)
                {
                    continue;
                }

                var totalPoints = 0d;

                totalPoints += Math.Min(Convert.ToDouble(player.Damage)  / 50000d, 10);
                totalPoints += Math.Min(Convert.ToDouble(player.Cleanses) / 100d, 5);
                totalPoints += Math.Min(Convert.ToDouble(player.Strips) / 30d, 3);
                totalPoints += Math.Min(Convert.ToDouble(player.StabUpTime) / 0.15d * (secondsOfFight < 30d ? 1d : secondsOfFight / 30d), 6);
                totalPoints += Math.Min(Convert.ToDouble(player.Healing) / 50000d, 4);
                totalPoints += Math.Min(Convert.ToDouble(player.Barrier) / 40000d, 3);

                totalPoints = Math.Max(4, Math.Min(12, totalPoints));

                account.Points += Convert.ToDecimal(totalPoints);
                account.AvailablePoints += Convert.ToDecimal(totalPoints);
                account.LastWvwLogDateTime = currentDateTimeUtc;
                _databaseContext.Update(account);
            }

            await _databaseContext.SaveChangesAsync();
        }
    }
}
