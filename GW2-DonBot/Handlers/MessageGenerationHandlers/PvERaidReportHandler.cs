using System.Globalization;
using Discord;
using Extensions;
using Models.Entities;
using Models.Enums;
using Models.Statics;

namespace Handlers.MessageGenerationHandlers
{
    public class PvERaidReportHandler
    {
        private readonly FooterHandler _footerHandler;
        private readonly DatabaseContext _databaseContext;

        public PvERaidReportHandler(FooterHandler footerHandler, DatabaseContext databaseContext)
        {
            _footerHandler = footerHandler;
            _databaseContext = databaseContext;
        }

        public Embed? Generate(FightsReport fightsReport)
        {
            if (fightsReport.FightsEnd == null)
            {
                return null;
            }

            var fights = _databaseContext.FightLog.Where(s => s.FightStart >= fightsReport.FightsStart && s.FightStart <= fightsReport.FightsEnd).OrderBy(s => s.FightStart).ToList();
            var playerFights = _databaseContext.PlayerFightLog.ToList(); 
            playerFights = playerFights.Where(s => fights.Select(f => f.FightLogId).Contains(s.FightLogId)).ToList();
            var groupedPlayerFights = playerFights.GroupBy(s => s.GuildWarsAccountName).OrderByDescending(s => s.Sum(d => d.Damage));
            var groupedFights = fights.GroupBy(f => f.FightType).OrderBy(f => f.Key);

            if (!fights.Any() || !playerFights.Any())
            {
                return null;
            }

            var firstFight = fights.First();
            var lastFight = fights.Last();

            var duration = lastFight.FightStart.AddMilliseconds(lastFight.FightDurationInMs) - firstFight.FightStart;
            var durationString = $"{(int)duration.TotalHours} hrs {(int)duration.TotalMinutes % 60} mins {duration.Seconds} secs";

            // Building the message via embeds
            var message = new EmbedBuilder
            {
                Title = "Report (PvE)\n",
                Description = $"**Length:** {durationString}\n",
                Color = (Color)System.Drawing.Color.FromArgb(123, 179, 91),
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
