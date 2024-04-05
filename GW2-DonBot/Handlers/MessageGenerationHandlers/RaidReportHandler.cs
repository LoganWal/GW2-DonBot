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

            var fights = _databaseContext.FightLog.Where(s => s.GuildId == guildId && s.FightStart >= fightsReport.FightsStart && s.FightStart <= new DateTime(2024, 04, 01)).OrderBy(s => s.FightStart).ToList();
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

            var fightsOverview = "```Fight    Total (t)    Success (t)     Attempts         \n";
            foreach (var groupedFight in groupedFights)
            {
                var fightsListForType = groupedFight.ToList();

                var fightTime = TimeSpan.FromMilliseconds(fightsListForType.Sum(s => s.FightDurationInMs));
                var fightTimeString = $"{fightTime.Minutes:D2}m:{fightTime.Seconds:D2}s";

                var successFightTime = TimeSpan.FromMilliseconds(fightsListForType.Where(s => s.IsSuccess).Sum(s => s.FightDurationInMs));
                var successFightTimeString = $"{successFightTime.Minutes:D2}m:{successFightTime.Seconds:D2}s";

                fightsOverview += $"{Enum.GetName(typeof(FightTypesEnum), groupedFight.Key)?.PadRight(4) ?? "uknwn"}{string.Empty,5}{fightTimeString,-6}{string.Empty,6}{successFightTimeString,-6}{string.Empty,9}{fightsListForType.Count}\n";
            }

            fightsOverview += "```";

            var playerOverview = "```Player           Dmg       Alac    Quick         \n";
            foreach (var groupedPlayerFight in groupedPlayerFights)
            {
                var playerFightsListForType = groupedPlayerFight.ToList();
                playerOverview += $"{groupedPlayerFight.Key?.ClipAt(13),-13}{string.Empty,4}{playerFightsListForType.Sum(s => s.Damage).FormatNumber(),-8}{string.Empty,2}{Math.Round(playerFightsListForType.Average(s => s.AlacDuration), 2).ToString(CultureInfo.CurrentCulture),-5}{string.Empty,3}{Math.Round(playerFightsListForType.Average(s => s.QuicknessDuration), 2).ToString(CultureInfo.CurrentCulture),-5}\n";
            }

            playerOverview += "```";

            message.AddField(x =>
            {
                x.Name = $"``` ```";
                x.Value = $"{fightsOverview.ReplaceSpacesWithNonBreaking()}";
                x.IsInline = false;
            });

            message.AddField(x =>
            {
                x.Name = $"``` ```";
                x.Value = $"{playerOverview.ReplaceSpacesWithNonBreaking()}";
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
