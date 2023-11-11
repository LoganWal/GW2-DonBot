using Discord;
using Extensions;
using Models;
using Models.Statics;

namespace Handlers.MessageGenerationHandlers
{
    public class PvEFightSummaryHandler
    {
        private readonly FooterHandler _footerHandler;

        public PvEFightSummaryHandler(FooterHandler footerHandler)
        {
            _footerHandler = footerHandler;
        }

        public Embed Generate(EliteInsightDataModel data)
        {
            // Building the actual message to be sent
            var logLength = data.EncounterDuration?.TimeToSeconds() ?? 0;

            var playerCount = data.Players?.Count ?? 0;

            var fightPhase = data.Phases?.Count > 0
                ? data.Phases[0]
                : new ArcDpsPhase();
            
            // Note this is against all targets (not just boss), need to do some more logic here to just get targets oof
            var sortedPlayerIndexByDamage = fightPhase.DpsStats? 
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
                    if (data.Targets != null)
                    {
                        foreach (var target in data.Targets)
                        {
                            if (target.Percent == 0)
                            {
                                break;
                            }
                            progress += target.Percent / 12;

                            showPhaseEmbed = true;
                            phaseProgress = target.Percent;
                            phaseName = target.Name;
                        }
                    }
                } break;

                // Standard
                default: progress = data.Targets?.First()?.Percent ?? 0; break;
            }

            var percentProgress = progress / 100.0f;

            var progressTitle = "";
            var progressOverview = "";

            progressTitle = $"Overall [{progress.FormatPercentage()}]".PadCenter(ArcDpsDataIndices.EmbedTitleCharacterLength);

            progressOverview = "```[";

            var initialCharacterLength = progressOverview.Length;
            var filledBarCount = (int)Math.Floor(percentProgress * ArcDpsDataIndices.EmbedBarCharacterLength);

            progressOverview = progressOverview.PadRight(filledBarCount + initialCharacterLength, '▰');
            progressOverview = progressOverview.PadRight(ArcDpsDataIndices.EmbedBarCharacterLength + initialCharacterLength, '▱');

            progressOverview += "]```";

            // Phase overview
            var phaseProgressTitle = "";
            var phaseProgressOverview = "";

            if (showPhaseEmbed)
            {
                var percentPhaseProgress = phaseProgress / 100.0f;

                phaseProgressTitle = $"{phaseName} [{phaseProgress.FormatPercentage()}]".PadCenter(ArcDpsDataIndices.EmbedTitleCharacterLength);

                phaseProgressOverview = "```[";

                var initialPhaseCharacterLength = phaseProgressOverview.Length;
                var phaseFilledBarCount = (int)Math.Floor(percentPhaseProgress * ArcDpsDataIndices.EmbedBarCharacterLength);

                phaseProgressOverview = phaseProgressOverview.PadRight(phaseFilledBarCount + initialPhaseCharacterLength, '▰');
                phaseProgressOverview = phaseProgressOverview.PadRight(ArcDpsDataIndices.EmbedBarCharacterLength + initialPhaseCharacterLength, '▱');

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

                damageOverview += $"{(index + 1).ToString().PadLeft(2, '0')}  {name?.ClipAt(ArcDpsDataIndices.NameClipLength) + EliteInsightExtensions.GetClassAppend(prof ?? string.Empty),-(ArcDpsDataIndices.NameSizeLength + 9)}  {(damageFloat / logLength).FormatNumber(maxDamage / logLength),6} \n";
            }

            damageOverview += "```";

            // Boon overview
            var boonOverview = "```";
            var squadBoons = new SquadBoons[51];

            for (var index = 0; index < playerCount; index++)
            {
                var playerSquad = data.Players?[index]?.Group ?? 0;

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

            if (showPhaseEmbed && (!data?.Success ?? false))
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
