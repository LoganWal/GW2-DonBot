using System.Globalization;
using Discord;
using Extensions;
using Models;

namespace Services.DiscordMessagingServices
{
    public class MessageGenerationService: IMessageGenerationService
    {
        private const int EnemyDamageIndex = 1;

        private const int FightPhaseIndex = 1;

        private const int FriendlyDownIndex = 8;

        private const int FriendlyDeathIndex = 10;

        private const int EnemyDeathIndex = 12;

        private const int EnemyDownIndex = 13;

        private const int PlayerCleansesIndex = 2;

        private const int PlayerStripsIndex = 4;

        private const int BoonStabDimension1Index = 8;

        private const int BoonStabDimension2Index = 1;

        private const int NameClipLength = 15;

        private const int NameSizeLength = 21;

        public Embed GenerateFightSummary(BotSecretsDataModel secrets, EliteInsightDataModel data)
        {
            // Building the actual message to be sent
            var logLength = data.EncounterDuration?.TimeToSeconds() ?? 0;

            var friendlyCount = data.Players?.Length ?? 0;
            var friendlyDamage = data.Players?.Sum(player => player.Details?.DmgDistributions?.FirstOrDefault()?.ContributedDamage) ?? 0;
            var friendlyDps = friendlyDamage / logLength;

            var enemyCount = data.Targets?.Length ?? 0;
            var enemyDamage = data.Targets?
                .Sum(player => player.Details?.DmgDistributions?.Length >= EnemyDamageIndex + 1
                    ? player.Details?.DmgDistributions?[EnemyDamageIndex].ContributedDamage
                    : 0) ?? 0;

            var enemyDps = enemyDamage / logLength;

            var fightPhase = data.Phases?.Length >= FightPhaseIndex + 1
                ? data.Phases[FightPhaseIndex]
                : new EliteInsightDataModelPhase();

            var friendlyDowns = fightPhase.DefStats?
                .Sum(playerDefStats => playerDefStats.Length >= FriendlyDownIndex + 1
                    ? playerDefStats[FriendlyDownIndex].DefStatValue
                    : 0) ?? 0;

            var friendlyDeaths = fightPhase.DefStats?
                .Sum(playerDefStats => playerDefStats.Length >= FriendlyDeathIndex + 1
                    ? playerDefStats[FriendlyDeathIndex].DefStatValue
                    : 0) ?? 0;

            var enemyDowns = fightPhase.OffensiveStatsTargets?
                .Sum(playerOffStats => playerOffStats.Sum(playerOffTargetStats => playerOffTargetStats?.Length >= EnemyDownIndex + 1
                    ? playerOffTargetStats?[EnemyDownIndex]
                    : 0)) ?? 0;

            var enemyDeaths = fightPhase.OffensiveStatsTargets?
                .Sum(playerOffStats => playerOffStats.Sum(playerOffTargetStats => playerOffTargetStats?.Length >= EnemyDeathIndex + 1
                    ? playerOffTargetStats?[EnemyDownIndex]
                    : 0)) ?? 0;

            var sortedPlayerIndexByDamage = fightPhase.DpsStats?
                .Select((value, index) => (Value: value.FirstOrDefault(), index))
                .OrderByDescending(x => x.Value)
                .ToDictionary(k => k.index, v => v.Value);

            var sortedPlayerIndexByCleanses = fightPhase.SupportStats?
                .Select((value, index) => (Value: value.FirstOrDefault() + (value.Length >= PlayerCleansesIndex + 1 ? value[PlayerCleansesIndex] : 0), index))
                .OrderByDescending(x => x.Value)
                .ToDictionary(k => k.index, v => v.Value);

            var sortedPlayerIndexByStrips = fightPhase.SupportStats?
                .Select((value, index) => (Value: value.Length >= PlayerStripsIndex + 1 ? value[PlayerStripsIndex] : 0, index))
                .OrderByDescending(x => x.Value)
                .ToDictionary(k => k.index, v => v.Value);

            var sortedPlayerIndexByStab = fightPhase.BoonStats?
                .Select((value, index) => (Value: value.Data?.CheckIndexIsValid(BoonStabDimension1Index, BoonStabDimension2Index) ?? false ? value.Data[BoonStabDimension1Index][BoonStabDimension2Index] : 0, index))
                .OrderByDescending(x => x.Value)
                .ToDictionary(k => k.index, v => v.Value);

            var friendlyCountStr = friendlyCount.ToString().PadCenter(7);
            var friendlyDamageStr = friendlyDamage.FormatNumber().PadCenter(7);
            var friendlyDPSStr = friendlyDps.FormatNumber().PadCenter(7);
            var friendlyDownsStr = friendlyDowns.ToString().PadCenter(7);
            var friendlyDeathsStr = friendlyDeaths.ToString().PadCenter(7);

            var enemyCountStr = enemyCount.ToString().PadCenter(7);
            var enemyDamageStr = enemyDamage.FormatNumber().PadCenter(7);
            var enemyDPSStr = enemyDps.FormatNumber().PadCenter(7);
            var enemyDownsStr = enemyDowns.ToString().PadCenter(7);
            var enemyDeathsStr = enemyDeaths.ToString().PadCenter(7);

            // Battleground parsing
            var range = (int)MathF.Min(15, data.FightName.Length - 1)..;
            var rangeStart = range.Start.GetOffset(data.FightName.Length);
            var rangeEnd = range.End.GetOffset(data.FightName.Length);

            if (rangeStart < 0 || rangeStart > data.FightName.Length || rangeEnd < 0 || rangeEnd > data.FightName.Length)
            {
                throw new Exception($"Bad battleground name: {data.FightName}");
            }

            var battleGround = data.FightName?[range] ?? string.Empty;

            var battleGroundEmoji = ":grey_question:";
            battleGroundEmoji = battleGround.Contains("Red", StringComparison.OrdinalIgnoreCase) ? ":red_square:" : battleGroundEmoji;
            battleGroundEmoji = battleGround.Contains("Blue", StringComparison.OrdinalIgnoreCase) ? ":blue_square:" : battleGroundEmoji;
            battleGroundEmoji = battleGround.Contains("Green", StringComparison.OrdinalIgnoreCase) ? ":green_square:" : battleGroundEmoji;
            battleGroundEmoji = battleGround.Contains("Eternal", StringComparison.OrdinalIgnoreCase) ? ":white_large_square:" : battleGroundEmoji;
            battleGroundEmoji = battleGround.Contains("Edge", StringComparison.OrdinalIgnoreCase) ? ":brown_square:" : battleGroundEmoji;

            var battleGroundColor = System.Drawing.Color.FromArgb(204, 214, 221);
            battleGroundColor = battleGround.Contains("Red", StringComparison.OrdinalIgnoreCase) ? System.Drawing.Color.FromArgb(219, 44, 67) : battleGroundColor;
            battleGroundColor = battleGround.Contains("Blue", StringComparison.OrdinalIgnoreCase) ? System.Drawing.Color.FromArgb(85, 172, 238) : battleGroundColor;
            battleGroundColor = battleGround.Contains("Green", StringComparison.OrdinalIgnoreCase) ? System.Drawing.Color.FromArgb(123, 179, 91) : battleGroundColor;
            battleGroundColor = battleGround.Contains("Eternal", StringComparison.OrdinalIgnoreCase) ? System.Drawing.Color.FromArgb(230, 231, 232) : battleGroundColor;
            battleGroundColor = battleGround.Contains("Edge", StringComparison.OrdinalIgnoreCase) ? System.Drawing.Color.FromArgb(193, 105, 79) : battleGroundColor;

            // Embed content building
            var friendlyOverview = "```Players  Damage     DPS     Downs   Deaths \n";
            friendlyOverview      += $"-------  -------  -------  -------  -------\n";
            friendlyOverview      += $"{friendlyCountStr}  {friendlyDamageStr}  {friendlyDPSStr}  {friendlyDownsStr}  {friendlyDeathsStr}```";

            var enemyOverview = "```Players  Damage     DPS     Downs   Deaths \n";
            enemyOverview      += $"-------  -------  -------  -------  -------\n";
            enemyOverview      += $"{enemyCountStr}  {enemyDamageStr}  {enemyDPSStr}  {enemyDownsStr}  {enemyDeathsStr}```";

            // Damage overview
            var damageOverview = "``` #           Name          Damage     DPS  \n";
            damageOverview      += $"-------------------------  -------  -------\n";

            var maxDamage = -1.0f;
            for (var index = 0; index < 5; index++)
            {
                if (index + 1 > sortedPlayerIndexByDamage?.Count)
                {
                    break;
                }

                if (sortedPlayerIndexByDamage?.ElementAt(index) == null)
                {
                    continue;
                }

                var damage = sortedPlayerIndexByDamage.ElementAt(index);
                var name = data.Players?[damage.Key].Name;
                var prof = data.Players?[damage.Key].Profession;
                var damageFloat = (float)damage.Value;
                if (maxDamage <= 0.0f)
                {
                    maxDamage = damageFloat;
                }

                damageOverview += $" {index + 1}  {(name?.ClipAt(NameClipLength) + EliteInsightExtensions.GetClassAppend(prof)).PadRight(NameSizeLength)}  {damageFloat.FormatNumber(maxDamage).PadCenter(7)}  {(damageFloat / logLength).FormatNumber(maxDamage / logLength).PadCenter(7)}\n";
            }

            damageOverview += "```";

            // Cleanse overview
            var cleanseOverview = $"``` #           Name              Cleanses    \n";
            cleanseOverview       += $"-------------------------  ----------------\n";

            for (var index = 0; index < 5; index++)
            {
                if (index + 1 > sortedPlayerIndexByCleanses?.Count)
                {
                    break;
                }

                if (sortedPlayerIndexByCleanses?.ElementAt(index) == null)
                {
                    continue;
                }

                var cleanses = sortedPlayerIndexByCleanses.ElementAt(index);
                var name = data.Players?[cleanses.Key].Name;
                var prof = data.Players?[cleanses.Key].Profession;

                cleanseOverview += $" {index + 1}  {(name?.ClipAt(NameClipLength) + EliteInsightExtensions.GetClassAppend(prof)).PadRight(NameSizeLength)}  {cleanses.Value.ToString(CultureInfo.InvariantCulture).PadCenter(16)}\n";
            }

            cleanseOverview += "```";

            // Strip overview
            var stripOverview = "``` #           Name               Strips     \n";
            stripOverview       += "-------------------------  ----------------\n";

            for (var index = 0; index < 5; index++)
            {
                if (index + 1 > sortedPlayerIndexByStrips?.Count)
                {
                    break;
                }

                if (sortedPlayerIndexByStrips?.ElementAt(index) == null)
                {
                    continue;
                }

                var strips = sortedPlayerIndexByStrips.ElementAt(index);
                var name = data.Players?[strips.Key].Name;
                var prof = data.Players?[strips.Key].Profession;

                stripOverview += $" {index + 1}  {(name?.ClipAt(NameClipLength) + EliteInsightExtensions.GetClassAppend(prof)).PadRight(NameSizeLength)}  {strips.Value.ToString(CultureInfo.InvariantCulture).PadCenter(16)}\n";
            }

            stripOverview += "```";

            // Stab overview
            var stabOverview = "``` #           Name          Sub     Uptime  \n";
            stabOverview       += "-------------------------  ---  -----------\n";

            for (var index = 0; index < 5; index++)
            {
                if (index + 1 > sortedPlayerIndexByStab?.Count)
                {
                    break;
                }

                if (sortedPlayerIndexByStab?.ElementAt(index) == null)
                {
                    continue;
                }

                var stab = sortedPlayerIndexByStab.ElementAt(index);
                var sub = data.Players[stab.Key].Group;
                var name = data.Players?[stab.Key].Name;
                var prof = data.Players?[stab.Key].Profession;

                stabOverview += $" {index + 1}  {(name?.ClipAt(NameClipLength) + EliteInsightExtensions.GetClassAppend(prof)).PadRight(NameSizeLength)}  {sub.ToString().PadCenter(3)}  {stab.Value.FormatPercentage().PadCenter(11)}\n";
            }

            stabOverview += "```";

            // Building the message via embeds
            var message = new EmbedBuilder
            {
                Title = $"{battleGroundEmoji} Report (WvW) - {battleGround}\n",
                Description = $"**Fight Duration:** {data?.EncounterDuration}\n",
                Color = (Color)battleGroundColor,
                Author = new EmbedAuthorBuilder()
                {
                    Name = "GW2-DonBot",
                    Url = "https://github.com/LoganWal/GW2-DonBot",
                    IconUrl = "https://i.imgur.com/tQ4LD6H.png"
                },
                Url = $"{secrets.ScrapedUrl}"
            };

            message.AddField(x =>
            {
                x.Name = "Friends";
                x.Value = $"{friendlyOverview}";
                x.IsInline = false;
            });

            message.AddField(x =>
            {
                x.Name = "Enemies";
                x.Value = $"{enemyOverview}";
                x.IsInline = false;
            });

            message.AddField(x =>
            {
                x.Name = "Damage";
                x.Value = $"{damageOverview}";
                x.IsInline = false;
            });

            message.AddField(x =>
            {
                x.Name = "Cleanses";
                x.Value = $"{cleanseOverview}";
                x.IsInline = false;
            });

            message.AddField(x =>
            {
                x.Name = "Strips";
                x.Value = $"{stripOverview}";
                x.IsInline = false;
            });

            message.AddField(x =>
            {
                x.Name = "Stability";
                x.Value = $"{stabOverview}";
                x.IsInline = false;
            });

            // Joke footers
            var footerMessageVariants = new[]
            {
                "This bot brought to you by PoE, ty Chris!",
                "Did you know SoX is a PvE PoE guild?",
                "I'm not supposed to be on the internet...",
                "Just in: Squirrel is a murderer.",
                "Always be straight licking that shit!",
                "SHEEEEEEEEEEEEEEEEEEEE!",
                "What do you like to tank on?",
                "Be the best dinker you can be!",
                "The fact you read this disgusts me.",
                "Alexa - make me a Discord bot.",
                "Yes, we raid on EVERY Thursday.",
                "Yes I'm vegan, yes I eat meat.",
                "This report is streets ahead.",
                "I can promise the real Don cleanses."
            };

            var rng = new Random();
            var footerVariantIndex = rng.Next(0, footerMessageVariants.Length);
            message.Footer = new EmbedFooterBuilder()
            {
                Text = $"{footerMessageVariants[footerVariantIndex]}",
                IconUrl = "https://i.imgur.com/tQ4LD6H.png"
            };

            // Timestamp
            message.Timestamp = DateTime.Now;

            // Building the message for use
            return message.Build();
        }
    }
}
