using Discord;
using Discord.WebSocket;
using Extensions;
using Models;
using Models.Entities;
using Services.SecretsServices;
using System.Globalization;

namespace Services.DiscordMessagingServices
{
    public class MessageGenerationService: IMessageGenerationService
    {
        private readonly ISecretService _secretService;

        public MessageGenerationService(ISecretService secretService)
        {
            _secretService = secretService;
        }

        private const int FriendlyDownIndex = 12;

        private const int FriendlyDeathIndex = 14;

        private const int EnemyDeathIndex = 12;

        private const int EnemyDownIndex = 13;

        private const int PlayerCleansesIndex = 2;

        private const int PlayerStripsIndex = 4;

        private const int BoonStabDimension1Index = 8;

        private const int BoonStabDimension2Index = 1;

        private const int HealingDimension1Index = 0;

        private const int HealingDimension2Index = 0;

        private const int NameClipLength = 15;

        private const int NameSizeLength = 21;

        private const int PlayersListed = 10;

        private const int EmbedTitleCharacterLength = 52;

        private const int EmbedBarCharacterLength = 23;

        private struct SquadBoons
        {
            public bool Initialized;

            public int PlayerCount;
            public int SquadNumber;
            
            public float MightStacks;
            public float FuryPercent;
            public float QuickPercent;
            public float AlacrityPercent;
            public float ProtectionPercent;
            public float RegenPercent;
            public float VigorPercent;
            public float AegisPercent;
            public float StabilityPercent;
            public float SwiftnessPercent;
            public float ResistancePercent;
            public float ResolutionPercent;

            public void AverageStats()
            {
                MightStacks /= PlayerCount;
                FuryPercent /= PlayerCount;
                QuickPercent /= PlayerCount;
                AlacrityPercent /= PlayerCount;
                ProtectionPercent /= PlayerCount;
                RegenPercent /= PlayerCount;
                VigorPercent /= PlayerCount;
                AegisPercent /= PlayerCount;
                StabilityPercent /= PlayerCount;
                SwiftnessPercent /= PlayerCount;
                ResistancePercent /= PlayerCount;
                ResolutionPercent /= PlayerCount;
            }
        }

        public Embed GenerateFightSummary(EliteInsightDataModel data, ulong guildId)
        {
            if (data.Wvw)
            {
                return GenerateWvWFightSummary(data);
            }
            else
            {
                return GeneratePvEFightSummary(data);
            }
        }

        public Embed GenerateWvWFightSummary(EliteInsightDataModel data)
        {
            // Set player points
            SetPlayerPoints(data);

            // Building the actual message to be sent
            var logLength = data.EncounterDuration?.TimeToSeconds() ?? 0;

            var friendlyCount = data.Players?.Count ?? 0;
            var friendlyDamage = data.Players?.Sum(player => player.Details?.DmgDistributions?.Sum(playerContribution => playerContribution.ContributedDamage)) ?? 0;
            var friendlyDps = friendlyDamage / logLength;

            var enemyCount = data.Targets?.Count ?? 0;
            var enemyDamage = data.Targets?
                .Sum(player => player.Details?.DmgDistributions?.Any() ?? false
                    ? player.Details?.DmgDistributions[0].ContributedDamage
                    : 0) ?? 0;

            var enemyDps = enemyDamage / logLength;

            var fightPhase = data.Phases?.Any() ?? false
                ? data.Phases[0]
                : new ArcDpsPhase();

            var friendlyDowns = fightPhase.DefStats?
                .Sum(playerDefStats => playerDefStats.Count >= FriendlyDownIndex
                    ? playerDefStats[FriendlyDownIndex].Double
                    : 0) ?? 0;

            var friendlyDeaths = fightPhase.DefStats?
                .Sum(playerDefStats => playerDefStats.Count >= FriendlyDeathIndex
                    ? playerDefStats[FriendlyDeathIndex].Double
                    : 0) ?? 0;

            var enemyDowns = fightPhase.OffensiveStatsTargets?
                .Sum(playerOffStats => playerOffStats.Sum(playerOffTargetStats => playerOffTargetStats?.Count >= EnemyDownIndex
                    ? playerOffTargetStats?[EnemyDownIndex]
                    : 0)) ?? 0;

            var enemyDeaths = fightPhase.OffensiveStatsTargets?
                .Sum(playerOffStats => playerOffStats.Sum(playerOffTargetStats => playerOffTargetStats?.Count >= EnemyDeathIndex
                    ? playerOffTargetStats?[EnemyDeathIndex]
                    : 0)) ?? 0;

            var sortedPlayerIndexByDamage = fightPhase.DpsStats?
                .Select((value, index) => (Value: value.FirstOrDefault(), index))
                .OrderByDescending(x => x.Value)
                .ToDictionary(k => k.index, v => v.Value);

            var sortedPlayerIndexByCleanses = fightPhase.SupportStats?
                .Select((value, index) => (Value: value.FirstOrDefault() + (value.Count >= PlayerCleansesIndex + 1 ? value[PlayerCleansesIndex] : 0), index))
                .OrderByDescending(x => x.Value)
                .ToDictionary(k => k.index, v => v.Value);

            var sortedPlayerIndexByStrips = fightPhase.SupportStats?
                .Select((value, index) => (Value: value.Count >= PlayerStripsIndex + 1 ? value[PlayerStripsIndex] : 0, index))
                .OrderByDescending(x => x.Value)
                .ToDictionary(k => k.index, v => v.Value);

            var sortedPlayerIndexByStab = fightPhase.BoonStats?
                .Select((value, index) => (Value: value.Data?.CheckIndexIsValid(BoonStabDimension1Index, BoonStabDimension2Index) ?? false ? value.Data[BoonStabDimension1Index][BoonStabDimension2Index] : 0, index))
                .OrderByDescending(x => x.Value)
                .ToDictionary(k => k.index, v => v.Value);

            var friendlyCountStr = friendlyCount.ToString().PadCenter(7);
            var friendlyDamageStr = friendlyDamage.FormatNumber().PadCenter(7);
            var friendlyDpsStr = friendlyDps.FormatNumber().PadCenter(7);
            var friendlyDownsStr = friendlyDowns.ToString().PadCenter(7);
            var friendlyDeathsStr = friendlyDeaths.ToString().PadCenter(7);

            var enemyCountStr = enemyCount.ToString().PadCenter(7);
            var enemyDamageStr = enemyDamage.FormatNumber().PadCenter(7);
            var enemyDpsStr = enemyDps.FormatNumber().PadCenter(7);
            var enemyDownsStr = enemyDowns.ToString().PadCenter(7);
            var enemyDeathsStr = enemyDeaths.ToString().PadCenter(7);

            // Battleground parsing
            var range = (int)MathF.Min(15, data.FightName?.Length - 1 ?? 0)..;
            var rangeStart = range.Start.GetOffset(data.FightName?.Length ?? 0);
            var rangeEnd = range.End.GetOffset(data.FightName?.Length ?? 0);

            if (rangeStart < 0 || rangeStart > data.FightName?.Length || rangeEnd < 0 || rangeEnd > data.FightName?.Length)
            {
                throw new Exception($"Bad battleground name: {data.FightName}");
            }

            var battleGround = data.FightName?[range] ?? string.Empty;

            var battleGroundEmoji = ":grey_question:";

            // Safe emojis -- these can be run in any Discord
            /*
            battleGroundEmoji = battleGround.Contains("Red", StringComparison.OrdinalIgnoreCase) ? ":red_square:" : battleGroundEmoji;
            battleGroundEmoji = battleGround.Contains("Blue", StringComparison.OrdinalIgnoreCase) ? ":blue_square:" : battleGroundEmoji;
            battleGroundEmoji = battleGround.Contains("Green", StringComparison.OrdinalIgnoreCase) ? ":green_square:" : battleGroundEmoji;
            battleGroundEmoji = battleGround.Contains("Eternal", StringComparison.OrdinalIgnoreCase) ? ":white_large_square:" : battleGroundEmoji;
            battleGroundEmoji = battleGround.Contains("Edge", StringComparison.OrdinalIgnoreCase) ? ":brown_square:" : battleGroundEmoji;
            */

            // SoX specific emojis -- these won't work elsewhere
            battleGroundEmoji = battleGround.Contains("Red", StringComparison.OrdinalIgnoreCase) ? "<:red_comm:1026849485780951090>" : battleGroundEmoji;
            battleGroundEmoji = battleGround.Contains("Blue", StringComparison.OrdinalIgnoreCase) ? "<:blue_comm:1026849483943837857>" : battleGroundEmoji;
            battleGroundEmoji = battleGround.Contains("Green", StringComparison.OrdinalIgnoreCase) ? "<:green_comm:1026849482043830272>" : battleGroundEmoji;
            battleGroundEmoji = battleGround.Contains("Eternal", StringComparison.OrdinalIgnoreCase) ? "<:grey_comm:1026849479325909063>" : battleGroundEmoji;
            battleGroundEmoji = battleGround.Contains("Edge", StringComparison.OrdinalIgnoreCase) ? "<:brown_comm:1026849476905816164>" : battleGroundEmoji;
            
            // To get custom emojis
            // Type \:emoji_name_here: and use the generated code below
            //battleGroundEmoji = "<:yellow_box:1026824406208618496>";
            //battleGroundEmoji = "<:red_comm:1026846907953328158>";

            var battleGroundColor = System.Drawing.Color.FromArgb(204, 214, 221);
            battleGroundColor = battleGround.Contains("Red", StringComparison.OrdinalIgnoreCase) ? System.Drawing.Color.FromArgb(219, 44, 67) : battleGroundColor;
            battleGroundColor = battleGround.Contains("Blue", StringComparison.OrdinalIgnoreCase) ? System.Drawing.Color.FromArgb(85, 172, 238) : battleGroundColor;
            battleGroundColor = battleGround.Contains("Green", StringComparison.OrdinalIgnoreCase) ? System.Drawing.Color.FromArgb(123, 179, 91) : battleGroundColor;
            battleGroundColor = battleGround.Contains("Eternal", StringComparison.OrdinalIgnoreCase) ? System.Drawing.Color.FromArgb(230, 231, 232) : battleGroundColor;
            battleGroundColor = battleGround.Contains("Edge", StringComparison.OrdinalIgnoreCase) ? System.Drawing.Color.FromArgb(193, 105, 79) : battleGroundColor;

            // Embed content building
            var friendlyOverview = "```";
            friendlyOverview      += $"{friendlyCountStr}  {friendlyDamageStr}  {friendlyDpsStr}  {friendlyDownsStr}  {friendlyDeathsStr}```";

            var enemyOverview = "```";
            enemyOverview      += $"{enemyCountStr}  {enemyDamageStr}  {enemyDpsStr}  {enemyDownsStr}  {enemyDeathsStr}```";

            // Damage overview
            var damageOverview = "```";

            var maxDamage = -1.0f;
            for (var index = 0; index < PlayersListed; index++)
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

                damageOverview += $"{(index + 1).ToString().PadLeft(2, '0')}  {(name?.ClipAt(NameClipLength) + EliteInsightExtensions.GetClassAppend(prof)).PadRight(NameSizeLength)}  {damageFloat.FormatNumber(maxDamage).PadCenter(7)}  {(damageFloat / logLength).FormatNumber(maxDamage / logLength).PadCenter(7)}\n";
            }

            damageOverview += "```";

            // Cleanse overview
            var cleanseOverview = $"```";

            for (var index = 0; index < PlayersListed; index++)
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

                cleanseOverview += $"{(index + 1).ToString().PadLeft(2, '0')}  {(name?.ClipAt(NameClipLength) + EliteInsightExtensions.GetClassAppend(prof)).PadRight(NameSizeLength)}  {cleanses.Value.ToString(CultureInfo.InvariantCulture).PadCenter(16)}\n";
            }

            cleanseOverview += "```";

            // Strip overview
            var stripOverview = "```";

            for (var index = 0; index < PlayersListed; index++)
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

                stripOverview += $"{(index + 1).ToString().PadLeft(2, '0')}  {(name?.ClipAt(NameClipLength) + EliteInsightExtensions.GetClassAppend(prof)).PadRight(NameSizeLength)}  {strips.Value.ToString(CultureInfo.InvariantCulture).PadCenter(16)}\n";
            }

            stripOverview += "```";

            // Stab overview
            var stabOverview = "```";

            for (var index = 0; index < PlayersListed; index++)
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
                var sub = data.Players?[stab.Key].Group;
                var name = data.Players?[stab.Key].Name;
                var prof = data.Players?[stab.Key].Profession;

                stabOverview += $"{(index + 1).ToString().PadLeft(2, '0')}  {(name?.ClipAt(NameClipLength) + EliteInsightExtensions.GetClassAppend(prof)).PadRight(NameSizeLength)}  {sub.ToString().PadCenter(3)}  {stab.Value.FormatPercentage().PadCenter(11)}\n";
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
                Url = $"{data?.Url}"
            };

            message.AddField(x =>
            {
                x.Name = "``` Friends    Damage      DPS       Downs     Deaths  ```";
                x.Value = $"{friendlyOverview}";
                x.IsInline = false;
            });

            message.AddField(x =>
            {
                x.Name = "``` Enemies    Damage      DPS       Downs     Deaths  ```";
                x.Value = $"{enemyOverview}";
                x.IsInline = false;
            });

            message.AddField(x =>
            {
                x.Name = "```  #            Name              Damage      DPS    ```";
                x.Value = $"{damageOverview}";
                x.IsInline = false;
            });

            message.AddField(x =>
            {
                x.Name = "```  #            Name                  Cleanses       ```";
                x.Value = $"{cleanseOverview}";
                x.IsInline = false;
            });

            message.AddField(x =>
            {
                x.Name = "```  #            Name                   Strips        ```";
                x.Value = $"{stripOverview}";
                x.IsInline = false;
            });

            /*
            message.AddField(x =>
            {
                x.Name = "Stability";
                x.Value = $"{stabOverview}";
                x.IsInline = false;
            });
            */

            message.Footer = new EmbedFooterBuilder()
            {
                Text = $"{GetJokeFooter()}",
                IconUrl = "https://i.imgur.com/tQ4LD6H.png"
            };

            // Timestamp
            message.Timestamp = DateTime.Now;

            // Building the message for use
            return message.Build();
        }

        public Embed GeneratePvEFightSummary(EliteInsightDataModel data)
        {
            // Building the actual message to be sent
            var logLength = data.EncounterDuration?.TimeToSeconds() ?? 0;

            var playerCount = data.Players?.Count ?? 0;

            var fightPhase = data.Phases?.Count > 0
                ? data.Phases[0]
                : new ArcDpsPhase();

            var sortedPlayerIndexByDamage = fightPhase.DpsStats? // Note this is against all targets (not just boss), need to do some more logic here to just get targets oof
                .Select((value, index) => (Value: value.FirstOrDefault(), index))
                .OrderByDescending(x => x.Value)
                .ToDictionary(k => k.index, v => v.Value);

            // Fight name parsing
            var fightEmoji = ":crossed_swords:";

            var fightColour = data.Success 
                              ? System.Drawing.Color.FromArgb(123, 179, 91) 
                              : System.Drawing.Color.FromArgb(219, 44, 67);
            // blue for later: System.Drawing.Color.FromArgb(85, 172, 238)

            // Progress
            var progress = 0.0f;
            var showPhaseEmbed = false;
            var phaseProgress = -1.0f;
            var phaseName = "";
            switch (data.FightName)
            {
                // HT CM
                case "The Dragonvoid CM":
                    {
                        foreach (var target in data.Targets)
                        {
                            if (target.Percent == 0)
                            {
                                break;
                            }
                            progress += (float)(target.Percent / 12);

                            showPhaseEmbed = true;
                            phaseProgress = (float)target.Percent;
                            phaseName = target.Name;
                        }
                    } break;
                // Standard
                default: progress = (float)data.Targets.First().Percent; break;
            }

            var percentProgress = progress / 100.0f;

            var progressTitle = "";
            var progressOverview = "";

            progressTitle = ($"Overall [{progress.FormatPercentage()}]").PadCenter(EmbedTitleCharacterLength);

            progressOverview = "```[";

            var initialCharacterLength = progressOverview.Length;
            var filledBarCount = (int)Math.Floor(percentProgress * EmbedBarCharacterLength);

            progressOverview = progressOverview.PadRight(filledBarCount + initialCharacterLength, '▰');
            progressOverview = progressOverview.PadRight(EmbedBarCharacterLength + initialCharacterLength, '▱');

            progressOverview += "]```";

            // Phase overview
            var phaseProgressTitle = "";
            var phaseProgressOverview = "";

            if (showPhaseEmbed)
            {
                var percentPhaseProgress = phaseProgress / 100.0f;

                phaseProgressTitle = ($"{phaseName} [{phaseProgress.FormatPercentage()}]").PadCenter(EmbedTitleCharacterLength);

                phaseProgressOverview = "```[";

                var initialPhaseCharacterLength = phaseProgressOverview.Length;
                var phaseFilledBarCount = (int)Math.Floor(percentPhaseProgress * EmbedBarCharacterLength);

                phaseProgressOverview = phaseProgressOverview.PadRight(phaseFilledBarCount + initialPhaseCharacterLength, '▰');
                phaseProgressOverview = phaseProgressOverview.PadRight(EmbedBarCharacterLength + initialPhaseCharacterLength, '▱');

                phaseProgressOverview += "]```";
            }

            // Damage overview
            var damageOverview = "```";

            var maxDamage = -1.0f;
            for (var index = 0; index < playerCount; index++)
            {
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

                damageOverview += $"{(index + 1).ToString().PadLeft(2, '0')}  {(name?.ClipAt(NameClipLength) + EliteInsightExtensions.GetClassAppend(prof)).PadRight(NameSizeLength + 9)}  {(damageFloat / logLength).FormatNumber(maxDamage / logLength).PadLeft(6)} \n";
            }

            damageOverview += "```";

            // Boon overview
            var boonOverview = "```";
            var squadBoons = new SquadBoons[51];

            for (var index = 0; index < playerCount; index++)
            {
                var playerSquad = data.Players[index].Group;

                squadBoons[playerSquad].Initialized = true;
                squadBoons[playerSquad].PlayerCount++;
                squadBoons[playerSquad].SquadNumber = (int)playerSquad;

                squadBoons[playerSquad].MightStacks +=          fightPhase?.BoonStats?[index].Data?.Count > 0  ? (float)(fightPhase?.BoonStats?[index].Data?[0]?.FirstOrDefault() ?? 0.0f) : 0.0f;
                squadBoons[playerSquad].FuryPercent +=          fightPhase?.BoonStats?[index].Data?.Count > 1  ? (float)(fightPhase?.BoonStats?[index].Data?[1]?.FirstOrDefault() ?? 0.0f) : 0.0f;
                squadBoons[playerSquad].QuickPercent +=         fightPhase?.BoonStats?[index].Data?.Count > 2  ? (float)(fightPhase?.BoonStats?[index].Data?[2]?.FirstOrDefault() ?? 0.0f) : 0.0f;
                squadBoons[playerSquad].AlacrityPercent +=      fightPhase?.BoonStats?[index].Data?.Count > 3  ? (float)(fightPhase?.BoonStats?[index].Data?[3]?.FirstOrDefault() ?? 0.0f) : 0.0f;
                squadBoons[playerSquad].ProtectionPercent +=    fightPhase?.BoonStats?[index].Data?.Count > 4  ? (float)(fightPhase?.BoonStats?[index].Data?[4]?.FirstOrDefault() ?? 0.0f) : 0.0f;
                squadBoons[playerSquad].RegenPercent +=         fightPhase?.BoonStats?[index].Data?.Count > 5  ? (float)(fightPhase?.BoonStats?[index].Data?[5]?.FirstOrDefault() ?? 0.0f) : 0.0f;
                squadBoons[playerSquad].VigorPercent +=         fightPhase?.BoonStats?[index].Data?.Count > 6  ? (float)(fightPhase?.BoonStats?[index].Data?[6]?.FirstOrDefault() ?? 0.0f) : 0.0f;
                squadBoons[playerSquad].AegisPercent +=         fightPhase?.BoonStats?[index].Data?.Count > 7  ? (float)(fightPhase?.BoonStats?[index].Data?[7]?.FirstOrDefault() ?? 0.0f) : 0.0f;
                squadBoons[playerSquad].StabilityPercent +=     fightPhase?.BoonStats?[index].Data?.Count > 8  ? (float)(fightPhase?.BoonStats?[index].Data?[8]?.FirstOrDefault() ?? 0.0f) : 0.0f;
                squadBoons[playerSquad].SwiftnessPercent +=     fightPhase?.BoonStats?[index].Data?.Count > 9  ? (float)(fightPhase?.BoonStats?[index].Data?[9]?.FirstOrDefault() ?? 0.0f) : 0.0f;
                squadBoons[playerSquad].ResistancePercent +=    fightPhase?.BoonStats?[index].Data?.Count > 10 ? (float)(fightPhase?.BoonStats?[index].Data?[10]?.FirstOrDefault() ?? 0.0f) : 0.0f;
                squadBoons[playerSquad].ResolutionPercent +=    fightPhase?.BoonStats?[index].Data?.Count > 11 ? (float)(fightPhase?.BoonStats?[index].Data?[11]?.FirstOrDefault() ?? 0.0f) : 0.0f;
            }

            var usedSquadBoons = new List<SquadBoons>();

            foreach (var boons in squadBoons)
            {
                if (boons.Initialized)
                {
                    boons.AverageStats();
                    usedSquadBoons.Add(boons);
                }
            }

            for (var index = 0; index < usedSquadBoons.Count; index++)
            {
                var squadNumber = usedSquadBoons[index].SquadNumber;
                var might = usedSquadBoons[index].MightStacks;
                var fury = usedSquadBoons[index].FuryPercent;
                var quick = usedSquadBoons[index].QuickPercent;
                var alac = usedSquadBoons[index].AlacrityPercent;
                var prot = usedSquadBoons[index].ProtectionPercent;
                var regen = usedSquadBoons[index].RegenPercent;

                boonOverview += $"{squadNumber.ToString().PadLeft(2, '0')}   ";
                boonOverview += $"{might.ToString("F1").PadCenter(4)}   ";
                boonOverview += $"{fury.FormatSimplePercentage().PadCenter(3)}    ";
                boonOverview += $"{alac.FormatSimplePercentage().PadCenter(3)}    ";
                boonOverview += $"{quick.FormatSimplePercentage().PadCenter(3)}    ";
                boonOverview += $"{prot.FormatSimplePercentage().PadCenter(3)}   ";
                boonOverview += $"{regen.FormatSimplePercentage().PadCenter(3)}\n";
            }

            boonOverview += "```";

            // Building the message via embeds
            var message = new EmbedBuilder
            {
                Title = $"{fightEmoji} Report (PvE) - {data.FightName}\n",
                Description = $"**Length:** {data?.EncounterDuration}\n**Group:** - Core | - Friends | - PuG",
                Color = (Color)fightColour,
                Author = new EmbedAuthorBuilder()
                {
                    Name = "GW2-DonBot",
                    Url = "https://github.com/LoganWal/GW2-DonBot",
                    IconUrl = "https://i.imgur.com/tQ4LD6H.png"
                },
                Url = $"{data?.Url}"
            };

            message.AddField(x =>
            {
                x.Name = $"```{progressTitle}```";
                x.Value = $"{progressOverview}";
                x.IsInline = false;
            });

            if (showPhaseEmbed && !data.Success)
            {
                message.AddField(x =>
                {
                    x.Name = $"```{phaseProgressTitle}```";
                    x.Value = $"{phaseProgressOverview}";
                    x.IsInline = false;
                });
            }

            message.AddField(x =>
            {
                x.Name = "``` #          Name                              DPS   ```";
                x.Value = $"{damageOverview}";
                x.IsInline = false;
            });

            message.AddField(x =>
            {
                x.Name = "``` #    Might    Fury    Alac    Quick   Prot    Rgn  ```";
                x.Value = $"{boonOverview}";
                x.IsInline = false;
            });

            message.Footer = new EmbedFooterBuilder()
            {
                Text = $"{GetJokeFooter()}",
                IconUrl = "https://i.imgur.com/tQ4LD6H.png"
            };

            // Timestamp
            message.Timestamp = DateTime.Now;

            // Building the message for use
            return message.Build();
        }

        public Embed GenerateWvWPlayerSummary(SocketGuild discordGuild, Guild gw2Guild)
        {
            List<Account> accounts;
            using (var context = new DatabaseContext().SetSecretService(_secretService))
            {
                accounts = context.Account.ToList();
            }

            var position = 1;

            var message = new EmbedBuilder
            {
                Title = "Report - WvW points **2x MULTIPLIER**\n",
                Description = "**WvW player Details:**\n",
                Color = (Color)System.Drawing.Color.FromArgb(230, 231, 232),
                Author = new EmbedAuthorBuilder()
                {
                    Name = "GW2-DonBot",
                    Url = "https://github.com/LoganWal/GW2-DonBot",
                    IconUrl = "https://i.imgur.com/tQ4LD6H.png"
                },
            };

            if (gw2Guild.DiscordGuildMemberRoleId != null)
            {
                var validAccounts = accounts.Where(account => discordGuild.GetUser((ulong)account.DiscordId)?.Roles.Select(role => role.Id).Contains((ulong)gw2Guild.DiscordGuildMemberRoleId) ?? false).ToList();

                for (var i = 0; i < (validAccounts.Count / 20) + 1; i++)
                {
                    var accountOverview = "```";
                    var useLimit = false;
                    var limit = 0;

                    if (position + 20 > validAccounts.Count)
                    {
                        limit = validAccounts.Count % 20;
                    }

                    foreach (var account in validAccounts.OrderByDescending(o => o.Points).Take(new Range(20 * i, !useLimit ? (20 * i) + 20 : limit)))
                    {
                        var points = account.Points;
                        var name = account.Gw2AccountName;
                        var pointsDiff = Math.Round(account.Points - account.PreviousPoints);

                        accountOverview += $"{position.ToString().PadLeft(3, '0')}  {name.ClipAt(NameSizeLength + 4).PadRight(NameSizeLength + 4)}  {Convert.ToInt32(points)}(+{Convert.ToInt32(pointsDiff)})\n";
                        position++;
                    }

                    accountOverview += "```";

                    message.AddField(x =>
                    {
                        x.Name = "```  #            Name                   Points        ```";
                        x.Value = $"{accountOverview}";
                        x.IsInline = false;
                    });
                }

                message.Footer = new EmbedFooterBuilder()
                {
                    Text = $"{GetJokeFooter()}",
                    IconUrl = "https://i.imgur.com/tQ4LD6H.png"
                };

                // Timestamp
                message.Timestamp = DateTime.Now;
            }

            // Building the message for use
            return message.Build();
        }

        public static string GetJokeFooter(int index = -1)
        {
            var footerMessageVariants = new[]
            {
                "This bot brought to you by PoE, ty Chris!",
                "Did you know SoX is a PvE PoE discordGuild?",
                "I'm not supposed to be on the internet...",
                "Just in: Squirrel is a murderer.",
                "Always be straight licking that shit!",
                "What do you like to tank on?",
                "Be the best dinker you can be!",
                "The fact you read this disgusts me.",
                "Alexa - make me a Discord bot.",
                "Yes, we raid on EVERY Thursday.",
                "Yes I'm vegan, yes I eat meat.",
                "This report is streets ahead.",
                "I can promise the real Don cleanses.",
                "I will turn you into horse glue.",
                "You are doing great, Kaye! - Squirrel",
                "You're right, Logan! - Squirrel",
                "Get on the seige!",
                "No one on the left cata!",
                "Those who cross Squirrel will die! Try a Dodge Roll!",
                "Never give up! Trust your training!",
                "Do your job!"
            };

            return index == -1 ?
                   footerMessageVariants[new Random().Next(0, footerMessageVariants.Length)] :
                   footerMessageVariants[Math.Min(index, footerMessageVariants.Length)];
        }

        private void SetPlayerPoints(EliteInsightDataModel eliteInsightDataModel)
        {
            List<Account> accounts;
            using (var context = new DatabaseContext().SetSecretService(_secretService))
            {
                accounts = context.Account.ToList();
            }

            if (!accounts.Any())
            {
                return;
            }

            var fightPhase = eliteInsightDataModel.Phases?.Any() ?? false
                ? eliteInsightDataModel.Phases[0]
                : new ArcDpsPhase();

            var healingPhase = eliteInsightDataModel.HealingStatsExtension?.HealingPhases?.Any() ?? false
                ? eliteInsightDataModel.HealingStatsExtension.HealingPhases[0]
                : new HealingPhase();

            var sortedPlayerIndexByDamage = fightPhase.DpsStats?
                .Select((value, index) => (Value: value.FirstOrDefault(), index))
                .OrderByDescending(x => x.Value)
                .ToDictionary(k => eliteInsightDataModel.Players?[k.index].Acc, v => v.Value);

            var sortedPlayerIndexByCleanses = fightPhase.SupportStats?
                .Select((value, index) => (Value: value.FirstOrDefault() + (value.Count >= PlayerCleansesIndex + 1 ? value[PlayerCleansesIndex] : 0), index))
                .OrderByDescending(x => x.Value)
                .ToDictionary(k => eliteInsightDataModel.Players?[k.index].Acc, v => v.Value);

            var sortedPlayerIndexByStrips = fightPhase.SupportStats?
                .Select((value, index) => (Value: value.Count >= PlayerStripsIndex + 1 ? value[PlayerStripsIndex] : 0, index))
                .OrderByDescending(x => x.Value)
                .ToDictionary(k => eliteInsightDataModel.Players?[k.index].Acc, v => v.Value);

            var sortedPlayerIndexByStab = fightPhase.BoonGenSquadStats?
                .Select((value, index) => (Value: value.Data?.CheckIndexIsValid(BoonStabDimension1Index, BoonStabDimension2Index) ?? false ? value.Data[BoonStabDimension1Index][BoonStabDimension2Index] : 0, index))
                .OrderByDescending(x => x.Value)
                .ToDictionary(k => eliteInsightDataModel.Players?[k.index].Acc, v => v.Value);

            var sortedPlayerIndexByHealing = healingPhase.OutgoingHealingStats?
                .Select((value, index) => (Value: value.FirstOrDefault(), index))
                .OrderByDescending(x => x.Value)
                .ToDictionary(k => eliteInsightDataModel.Players?[k.index].Acc, v => v.Value);

            var stringDuration = eliteInsightDataModel.EncounterDuration;

            var secondsOfFight = 0;
            if (!string.IsNullOrEmpty(stringDuration))
            {
                try
                {
                    var minutes = Convert.ToInt32(stringDuration.Substring(0, 2));
                    var seconds = Convert.ToInt32(stringDuration.Substring(4, 2));
                    secondsOfFight = (minutes * 60) + seconds;
                }
                catch (Exception)
                {
                    Console.WriteLine($"Failed to parse to seconds of fight `{stringDuration}`");
                }
            }

            using (var context = new DatabaseContext().SetSecretService(_secretService))
            {
                var pointsPerCategory = 5;
                var stabPointsCap = 6;
                var healingPointsCap = 6;

                foreach (var account in accounts)
                {
                    account.PreviousPoints = account.Points;
                }

                context.UpdateRange(accounts);
                context.SaveChanges();

                foreach (var player in eliteInsightDataModel.Players)
                {
                    var account = accounts.FirstOrDefault(a => a.Gw2AccountName == player.Acc);
                    if (account == null)
                    {
                        continue;
                    }

                    var totalPoints = 0d;

                    if (sortedPlayerIndexByDamage.TryGetValue(account.Gw2AccountName, out var damage))
                    {
                        var damagePoints = damage / 80000;
                        totalPoints += damagePoints > pointsPerCategory ? pointsPerCategory : damagePoints;
                    }

                    if (sortedPlayerIndexByCleanses.TryGetValue(account.Gw2AccountName, out var cleanses))
                    {
                        var cleansePoints = cleanses / 100;
                        totalPoints += cleansePoints > pointsPerCategory ? pointsPerCategory : cleansePoints;
                    }

                    if (sortedPlayerIndexByStrips.TryGetValue(account.Gw2AccountName, out var strips))
                    {
                        var stripPoints = strips / 20;
                        totalPoints += stripPoints > pointsPerCategory ? pointsPerCategory : stripPoints;
                    }

                    if (sortedPlayerIndexByStab.TryGetValue(account.Gw2AccountName, out var stab))
                    {
                        var stabMultiplier = secondsOfFight < 30 ? 1 : secondsOfFight / 30;
                        var stabPoint = (stab / 0.15) * stabMultiplier;
                        totalPoints += stabPoint > stabPointsCap ? stabPointsCap : stabPoint;
                    }

                    if (sortedPlayerIndexByHealing.TryGetValue(account.Gw2AccountName, out var healing))
                    {
                        var healingPoints = healing / 50000;
                        totalPoints += healingPoints > healingPointsCap ? healingPointsCap : healingPoints;
                    }

                    if (totalPoints < 4)
                    {
                        totalPoints = 4;
                    }

                    totalPoints *= 2;

                    account.Points += Convert.ToDecimal(totalPoints);
                    account.AvailablePoints += Convert.ToDecimal(totalPoints);
                    context.Update(account);
                }

                context.SaveChanges();
            }
        }
    }
}