using Discord;
using Testing.Models;

namespace Services.DiscordBase
{
    public class MessageGenerationService
    {
        public Embed GenerateFightSummary(BotSecretsDataModel secrets, EliteInsightDataModel data)
        {
            /*
            // -- Exploring the offensive stats breakdown
            /*
            var phase = data?.Phases[1];
            var players = data?.Players;
            string test2 = "";
            for (int i = 0; i < players.Length; i++)
            {
                test2 += $"{players[i].Name}:\n";

                //test2 += $"[{i}] Avg: {phase.BoonStats[i].Avg}\n";
                for (int x = 0; x < phase.OffensiveStats[i].Length; x++)
                {
                    test2 += $"[{x}]: ";
                    test2 += $"{phase.OffensiveStats[i][x]}";
                    test2 += $"\n";
                    //for (int y = 0; y < phase.OffensiveStats[i][x].Length; y++)
                    //{
                    //    test2 += $"[{x},{y}]: ";
                    //    test2 += $"{phase.OffensiveStats[i][x][y]}";
                    //    test2 += $"\n";
                    //}
                }
            }
            */

            // Building the actual message to be sent
            var logLength = data?.EncounterDuration.TimeToSeconds();

            var friendlyCount = data?.Players?.Length;
            var friendlyDamage = data?.Players?.ToList()?.Sum(player => player.Details.DmgDistributions[0].ContributedDamage);
            var friendlyDPS = (float)friendlyDamage / (float)logLength;

            var enemyCount = data?.Targets?.Length;
            var enemyDamage = data?.Targets?.ToList()?.Sum(player => player.Details.DmgDistributions[1].ContributedDamage);
            var enemyDPS = (float)enemyDamage / (float)logLength;

            var friendlyDowns = data?.Phases[1].DefStats.ToList().Sum(playerDefStats => playerDefStats[8].Integer);
            var friendlyDeaths = data?.Phases[1].DefStats.ToList().Sum(playerDefStats => playerDefStats[10].Integer);

            var enemyDowns = data?.Phases[1].OffensiveStatsTargets.ToList().Sum(playerOffStats => playerOffStats[0][13]);
            var enemyDeaths = data?.Phases[1].OffensiveStatsTargets.ToList().Sum(playerOffStats => playerOffStats[0][12]);

            var sortedByDamage = data?.Phases[1].DpsStats.ToList().Select((playerStat, i) => new KeyValuePair<double, int>(playerStat[0], i)).OrderByDescending(playerStat => playerStat.Key).ToList();
            var damageValues = sortedByDamage?.Select(x => x.Key).ToList();
            var damageIndices = sortedByDamage?.Select(x => x.Value).ToList();

            var sortedByCleanses = data?.Phases[1].SupportStats.ToList().Select((playerStat, i) => new KeyValuePair<double, int>(playerStat[0] + playerStat[2], i)).OrderByDescending(playerStat => playerStat.Key).ToList();
            var cleanseValues = sortedByCleanses?.Select(x => x.Key).ToList();
            var cleanseIndices = sortedByCleanses?.Select(x => x.Value).ToList();

            var sortedByStrips = data?.Phases[1].SupportStats.ToList().Select((playerStat, i) => new KeyValuePair<double, int>(playerStat[4], i)).OrderByDescending(playerStat => playerStat.Key).ToList();
            var stripValues = sortedByStrips?.Select(x => x.Key).ToList();
            var stripIndices = sortedByStrips?.Select(x => x.Value).ToList();

            var sortedByStab = data?.Phases[1].BoonStats.ToList().Select((playerStat, i) => new KeyValuePair<double, int>(playerStat.Data[8] != null && playerStat.Data[8].Length >= 2 ? playerStat.Data[8][1] : 0.0, i)).OrderByDescending(playerSupStat => playerSupStat.Key).ToList();
            var stabValues = sortedByStab?.Select(x => x.Key).ToList();
            var stabIndices = sortedByStab?.Select(x => x.Value).ToList();

            var friendlyCountStr = friendlyCount?.ToString().PadCenter(7);
            var friendlyDamageStr = friendlyDamage?.FormatNumber().PadCenter(7);
            var friendlyDPSStr = friendlyDPS.FormatNumber().PadCenter(7);
            var friendlyDownsStr = friendlyDowns?.ToString().PadCenter(7);
            var friendlyDeathsStr = friendlyDeaths?.ToString().PadCenter(7);

            var enemyCountStr = enemyCount?.ToString().PadCenter(7);
            var enemyDamageStr = enemyDamage?.FormatNumber().PadCenter(7);
            var enemyDPSStr = enemyDPS.FormatNumber().PadCenter(7);
            var enemyDownsStr = enemyDowns?.ToString().PadCenter(7);
            var enemyDeathsStr = enemyDeaths?.ToString().PadCenter(7);

            // Battleground parsing
            var battleGround = data?.FightName.Substring(15);

            var battleGroundEmoji = ":grey_question:";
            battleGroundEmoji = battleGround.Contains("Red") ? ":red_square:" : battleGroundEmoji;
            battleGroundEmoji = battleGround.Contains("Blue") ? ":blue_square:" : battleGroundEmoji;
            battleGroundEmoji = battleGround.Contains("Green") ? ":green_square:" : battleGroundEmoji;
            battleGroundEmoji = battleGround.Contains("Eternal") ? ":white_large_square:" : battleGroundEmoji;

            var battleGroundColor = System.Drawing.Color.Gray;
            battleGroundColor = battleGround.Contains("Red") ? System.Drawing.Color.FromArgb(219, 44, 67) : battleGroundColor;
            battleGroundColor = battleGround.Contains("Blue") ? System.Drawing.Color.FromArgb(85, 172, 238) : battleGroundColor;
            battleGroundColor = battleGround.Contains("Green") ? System.Drawing.Color.FromArgb(123, 179, 91) : battleGroundColor;
            battleGroundColor = battleGround.Contains("Eternal") ? System.Drawing.Color.FromArgb(230, 231, 232) : battleGroundColor;

            // Embed content building
            var friendlyOverview = "";
            friendlyOverview += $"```Players  Damage     DPS     Downs   Deaths \n";
            friendlyOverview += $"-------  -------  -------  -------  -------\n";
            friendlyOverview += $"{friendlyCountStr}  {friendlyDamageStr}  {friendlyDPSStr}  {friendlyDownsStr}  {friendlyDeathsStr}```";

            var enemyOverview = "";
            enemyOverview += $"```Players  Damage     DPS     Downs   Deaths \n";
            enemyOverview += $"-------  -------  -------  -------  -------\n";
            enemyOverview += $"{enemyCountStr}  {enemyDamageStr}  {enemyDPSStr}  {enemyDownsStr}  {enemyDeathsStr}```";

            var damageOverview = "";
            damageOverview += $"``` #           Name          Damage     DPS  \n";
            damageOverview += $"---  --------------------  -------  -------\n";
            damageOverview += $" 1   {data?.Players[damageIndices[0]].Name.PadRight(20)}  {((float)damageValues[0]).FormatNumber((float)damageValues[0]).PadCenter(7)}  {((float)(damageValues[0] / logLength)).FormatNumber((float)(damageValues[0] / logLength)).PadCenter(7)}\n";
            damageOverview += $" 2   {data?.Players[damageIndices[1]].Name.PadRight(20)}  {((float)damageValues[1]).FormatNumber((float)damageValues[0]).PadCenter(7)}  {((float)(damageValues[1] / logLength)).FormatNumber((float)(damageValues[0] / logLength)).PadCenter(7)}\n";
            damageOverview += $" 3   {data?.Players[damageIndices[2]].Name.PadRight(20)}  {((float)damageValues[2]).FormatNumber((float)damageValues[0]).PadCenter(7)}  {((float)(damageValues[2] / logLength)).FormatNumber((float)(damageValues[0] / logLength)).PadCenter(7)}\n";
            damageOverview += $" 4   {data?.Players[damageIndices[3]].Name.PadRight(20)}  {((float)damageValues[3]).FormatNumber((float)damageValues[0]).PadCenter(7)}  {((float)(damageValues[3] / logLength)).FormatNumber((float)(damageValues[0] / logLength)).PadCenter(7)}\n";
            damageOverview += $" 5   {data?.Players[damageIndices[4]].Name.PadRight(20)}  {((float)damageValues[4]).FormatNumber((float)damageValues[0]).PadCenter(7)}  {((float)(damageValues[4] / logLength)).FormatNumber((float)(damageValues[0] / logLength)).PadCenter(7)}```";

            var cleanseOverview = "";
            cleanseOverview += $"``` #           Name              Cleanses    \n";
            cleanseOverview += $"---  --------------------  ----------------\n";
            cleanseOverview += $" 1   {data?.Players[cleanseIndices[0]].Name.PadRight(20)}  {cleanseValues[0].ToString().PadCenter(16)}\n";
            cleanseOverview += $" 2   {data?.Players[cleanseIndices[1]].Name.PadRight(20)}  {cleanseValues[1].ToString().PadCenter(16)}\n";
            cleanseOverview += $" 3   {data?.Players[cleanseIndices[2]].Name.PadRight(20)}  {cleanseValues[2].ToString().PadCenter(16)}\n";
            cleanseOverview += $" 4   {data?.Players[cleanseIndices[3]].Name.PadRight(20)}  {cleanseValues[3].ToString().PadCenter(16)}\n";
            cleanseOverview += $" 5   {data?.Players[cleanseIndices[4]].Name.PadRight(20)}  {cleanseValues[4].ToString().PadCenter(16)}```";

            var stripOverview = "";
            stripOverview += $"``` #           Name               Strips     \n";
            stripOverview += $"---  --------------------  ----------------\n";
            stripOverview += $" 1   {data?.Players[stripIndices[0]].Name.PadRight(20)}  {stripValues[0].ToString().PadCenter(16)}\n";
            stripOverview += $" 2   {data?.Players[stripIndices[1]].Name.PadRight(20)}  {stripValues[1].ToString().PadCenter(16)}\n";
            stripOverview += $" 3   {data?.Players[stripIndices[2]].Name.PadRight(20)}  {stripValues[2].ToString().PadCenter(16)}\n";
            stripOverview += $" 4   {data?.Players[stripIndices[3]].Name.PadRight(20)}  {stripValues[3].ToString().PadCenter(16)}\n";
            stripOverview += $" 5   {data?.Players[stripIndices[4]].Name.PadRight(20)}  {stripValues[4].ToString().PadCenter(16)}```";

            var stabOverview = "";
            stabOverview += $"``` #           Name               Uptime     \n";
            stabOverview += $"---  --------------------  ----------------\n";
            stabOverview += $" 1   {data?.Players[stabIndices[0]].Name.PadRight(20)}  {stabValues[0].FormatPercentage().PadCenter(16)}\n";
            stabOverview += $" 2   {data?.Players[stabIndices[1]].Name.PadRight(20)}  {stabValues[1].FormatPercentage().PadCenter(16)}\n";
            stabOverview += $" 3   {data?.Players[stabIndices[2]].Name.PadRight(20)}  {stabValues[2].FormatPercentage().PadCenter(16)}\n";
            stabOverview += $" 4   {data?.Players[stabIndices[3]].Name.PadRight(20)}  {stabValues[3].FormatPercentage().PadCenter(16)}\n";
            stabOverview += $" 5   {data?.Players[stabIndices[4]].Name.PadRight(20)}  {stabValues[4].FormatPercentage().PadCenter(16)}```";

            var message = new EmbedBuilder();
            message.Title = $"{battleGroundEmoji} Report (WvW) - {battleGround}\n";
            message.Description = $"**Fight Duration:** {data?.EncounterDuration}\n";
            message.Color = (Discord.Color)battleGroundColor;
            message.Author = new EmbedAuthorBuilder() { Name = "GW2-DonBot", Url = "https://github.com/LoganWal/GW2-DonBot", IconUrl = "https://i.imgur.com/tQ4LD6H.png" };
            message.Url = $"{secrets.ScrapedUrl}";

            message.AddField(x => { x.Name = "Friends"; x.Value = $"{friendlyOverview}"; x.IsInline = false; });
            message.AddField(x => { x.Name = "Enemies"; x.Value = $"{enemyOverview}"; x.IsInline = false; });
            message.AddField(x => { x.Name = "Damage"; x.Value = $"{damageOverview}"; x.IsInline = false; });
            message.AddField(x => { x.Name = "Cleanses"; x.Value = $"{cleanseOverview}"; x.IsInline = false; });
            message.AddField(x => { x.Name = "Strips"; x.Value = $"{stripOverview}"; x.IsInline = false; });
            //message.AddField(x => { x.Name = "Stability"; x.Value = $"{stabOverview}"; x.IsInline = false; });

            // Joke footers
            var footerMessageVariants = new string[]
            {
                "This bot brought to you by PoE, ty Chris!",
                "Did you know SoX is a PvE PoE guild?",
                "I'm not supposed to be on the internet...",
                "Just in: Squirrel is a murderer.",
                "Always be straight licking that shit!",
                "SHEEEEEEEEEEEEEEEEEEEE",
                "What do you like to tank on?",
                "Be the best dinker you can be!",
                "The fact you read this disgusts me.",
                "Alexa - make me a Discord bot.",
                "Yes, we raid on EVERY Thursday.",
                "Yes I'm vegan, yes I eat meat."
            };

            var rng = new Random();
            var footerVariantIndex = rng.Next(0, footerMessageVariants.Length);
            message.Footer = new EmbedFooterBuilder() { Text = $"{footerMessageVariants[footerVariantIndex]}", IconUrl = "https://i.imgur.com/tQ4LD6H.png" };
            message.Timestamp = DateTime.Now;

            return message.Build();
        }
    }
}
