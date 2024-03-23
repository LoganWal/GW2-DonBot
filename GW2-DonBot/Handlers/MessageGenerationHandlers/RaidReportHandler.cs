using System.Globalization;
using Discord;
using Extensions;
using Models;
using Models.Entities;
using Models.Enums;
using Models.Statics;

namespace Handlers.MessageGenerationHandlers
{
    public class RaidReportHandler
    {
        private readonly FooterHandler _footerHandler;
        private readonly DatabaseContext _databaseContext;
        private readonly WvWFightSummaryHandler _wvWFightSummaryHandler;

        public RaidReportHandler(FooterHandler footerHandler, DatabaseContext databaseContext, WvWFightSummaryHandler wvWFightSummaryHandler)
        {
            _footerHandler = footerHandler;
            _databaseContext = databaseContext;
            _wvWFightSummaryHandler = wvWFightSummaryHandler;
        }

        public List<Embed>? Generate(FightsReport fightsReport, long guildId)
        {
            var messages = new List<Embed>();
            if (fightsReport.FightsEnd == null)
            {
                return null;
            }

            var fights = _databaseContext.FightLog.Where(s => s.GuildId == guildId && s.FightStart >= fightsReport.FightsStart && s.FightStart <= fightsReport.FightsEnd).OrderBy(s => s.FightStart).ToList();
            var playerFights = _databaseContext.PlayerFightLog.ToList(); 
            playerFights = playerFights.Where(s => fights.Select(f => f.FightLogId).Contains(s.FightLogId)).ToList();
            var groupedPlayerFights = playerFights.GroupBy(s => s.GuildWarsAccountName).OrderByDescending(s => s.Sum(d => d.Damage)).ToList();
            var groupedFights = fights.GroupBy(f => f.FightType).OrderBy(f => f.Key);

            if (!fights.Any() || !playerFights.Any())
            {
                return null;
            }

            var firstFight = fights.First();
            var lastFight = fights.Last();

            var duration = lastFight.FightStart.AddMilliseconds(lastFight.FightDurationInMs) - firstFight.FightStart;
            var durationString = $"{(int)duration.TotalHours} hrs {(int)duration.TotalMinutes % 60} mins {duration.Seconds} secs";

            var wvwFightCount = fights.Count(s => s.FightType == (short)FightTypesEnum.wvw && s.FightType != (short)FightTypesEnum.unkn);
            var pveFightCount = fights.Count(s => s.FightType != (short)FightTypesEnum.wvw && s.FightType != (short)FightTypesEnum.unkn);

            if (wvwFightCount > pveFightCount)
            {
                messages.Add(GenerateWvWRaidReport(durationString, groupedPlayerFights, false));
                messages.Add(GenerateWvWRaidReport(durationString, groupedPlayerFights, true));

            }
            else
            {
                messages.Add(GeneratePvERaidReport(durationString, groupedFights, groupedPlayerFights));
            }

            return messages;
        }

        private Embed GenerateWvWRaidReport(string durationString, List<IGrouping<string, PlayerFightLog>> groupedPlayerFights, bool advancedLog)
        {
            // Building the message via embeds
            var message = new EmbedBuilder
            {
                Title = "Report (WvW)\n",
                Description = $"**Length:** {durationString}\n",
                Color = (Color)System.Drawing.Color.FromArgb(195, 0, 101),
                Author = new EmbedAuthorBuilder()
                {
                    Name = "GW2-DonBot",
                    Url = "https://github.com/LoganWal/GW2-DonBot",
                    IconUrl = "https://i.imgur.com/tQ4LD6H.png"
                }
            };

            var gw2Players = new List<Gw2Player>();
            foreach (var groupedPlayerFight in groupedPlayerFights)
            {
                var playersFights = groupedPlayerFight.ToList();
                var player = playersFights.First();
                gw2Players.Add(new Gw2Player
                {
                    AccountName = player.GuildWarsAccountName,
                    SubGroup = player.SubGroup,
                    Damage = playersFights.Sum(s => s.Damage),
                    DamageDownContribution = playersFights.Sum(s => s.DamageDownContribution),
                    Cleanses = playersFights.Sum(s => s.Cleanses),
                    Strips = playersFights.Sum(s => s.Strips),
                    StabUpTime = Math.Round(Convert.ToDouble(playersFights.Average(s => s.StabGenerated)), 2),
                    Healing = playersFights.Sum(s => s.Healing),
                    BarrierGenerated = playersFights.Sum(s => s.BarrierGenerated),
                    DistanceFromTag = Math.Round(Convert.ToDouble(playersFights.Average(s => s.DistanceFromTag)), 2),
                    TimesDowned = playersFights.Sum(s => s.TimesDowned),
                    Interrupts = playersFights.Sum(s => s.Interrupts),
                    NumberOfHitsWhileBlinded = playersFights.Sum(s => s.NumberOfHitsWhileBlinded),
                    NumberOfMissesAgainst = playersFights.Sum(s => s.NumberOfMissesAgainst),
                    NumberOfTimesBlockedAttack = playersFights.Sum(s => s.NumberOfTimesBlockedAttack),
                    NumberOfTimesEnemyBlockedAttack = playersFights.Sum(s => s.NumberOfTimesEnemyBlockedAttack),
                    NumberOfBoonsRipped = playersFights.Sum(s => s.NumberOfBoonsRipped),
                    DamageTaken = playersFights.Sum(s => s.DamageTaken),
                    BarrierMitigation = playersFights.Sum(s => s.BarrierMitigation),
                    TotalQuick = Math.Round(Convert.ToDouble(playersFights.Average(s => s.QuicknessDuration)), 2),
                    TotalAlac = Math.Round(Convert.ToDouble(playersFights.Average(s => s.QuicknessDuration)), 2)
                });
            }

            message.Footer = new EmbedFooterBuilder()
            {
                Text = $"{_footerHandler.Generate()}",
                IconUrl = "https://i.imgur.com/tQ4LD6H.png"
            };

            // Timestamp
            message.Timestamp = DateTime.Now;

            // Building the message for use
            return _wvWFightSummaryHandler.GenerateMessage(advancedLog, 5, gw2Players, message);
        }

        private Embed GeneratePvERaidReport(string durationString, IOrderedEnumerable<IGrouping<short, FightLog>> groupedFights, List<IGrouping<string, PlayerFightLog>> groupedPlayerFights)
        {
            // Building the message via embeds
            var message = new EmbedBuilder
            {
                Title = "Report (PvE)\n",
                Description = $"**Length:** {durationString}\n",
                Color = (Color)System.Drawing.Color.FromArgb(195, 0, 101),
                Author = new EmbedAuthorBuilder()
                {
                    Name = "GW2-DonBot",
                    Url = "https://github.com/LoganWal/GW2-DonBot",
                    IconUrl = "https://i.imgur.com/tQ4LD6H.png"
                }
            };

            var fightsOverview = "```";
            foreach (var groupedFight in groupedFights)
            {
                var fightsListForType = groupedFight.ToList();
                fightsOverview += $"{Enum.GetName(typeof(FightTypesEnum), groupedFight.Key)?.PadLeft(5) ?? "uknwn"}   {(fightsListForType.Sum(s => s.FightDurationInMs)/1000).ToString().PadLeft(4, '0')}              {(fightsListForType.Where(s => s.IsSuccess).Sum(s => s.FightDurationInMs) / 1000).ToString().PadLeft(4, '0')}                {fightsListForType.Count}\n";
            }

            fightsOverview += "```";

            var playerOverview = "```";
            foreach (var groupedPlayerFight in groupedPlayerFights)
            {
                var playerFightsListForType = groupedPlayerFight.ToList();
                playerOverview += $"{groupedPlayerFight.Key?.ClipAt(13),-13}  {playerFightsListForType.Sum(s => s.Damage),9}      {Math.Round(playerFightsListForType.Average(s => s.AlacDuration), 2).ToString(CultureInfo.CurrentCulture).PadRight(5, '0')}          {Math.Round(playerFightsListForType.Average(s => s.QuicknessDuration), 2).ToString(CultureInfo.CurrentCulture).PadRight(5, '0')}\n";
            }

            playerOverview += "```";

            message.AddField(x =>
            {
                x.Name = $"``` Fight    Total Duration(s)    Success Duration(s)     Attempts ```";
                x.Value = $"{fightsOverview}";
                x.IsInline = false;
            });

            message.AddField(x =>
            {
                x.Name = $"``` Player            Total Dmg        Avg Alac          Avg Quick ```";
                x.Value = $"{playerOverview}";
                x.IsInline = false;
            });

            message.Footer = new EmbedFooterBuilder()
            {
                Text = $"{_footerHandler.Generate()}",
                IconUrl = "https://i.imgur.com/tQ4LD6H.png"
            };

            // Timestamp
            message.Timestamp = DateTime.Now;

            // Building the message for use
            return message.Build();
        }
    }
}
