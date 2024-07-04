using Discord;
using Extensions;
using Models;
using Models.Entities;
using Models.Enums;
using Models.Statics;
using Services.PlayerServices;
using System.Globalization;

namespace Handlers.MessageGenerationHandlers
{
    public class PvEFightSummaryHandler
    {
        private readonly FooterHandler _footerHandler;
        private readonly IPlayerService _playerService;
        private readonly DatabaseContext _databaseContext;

        public PvEFightSummaryHandler(FooterHandler footerHandler, IPlayerService playerService, DatabaseContext databaseContext)
        {
            _footerHandler = footerHandler;
            _playerService = playerService;
            _databaseContext = databaseContext;
        }

        public Embed Generate(EliteInsightDataModel data, ulong guildId)
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

                damageOverview += $"{(index + 1).ToString().PadLeft(2, '0')}  {(name + EliteInsightExtensions.GetClassAppend(prof ?? string.Empty)).ClipAt(ArcDpsDataIndices.NameSizeLength),-(ArcDpsDataIndices.NameSizeLength + 9)}  {(damageFloat / logLength).FormatNumber(maxDamage / logLength),6} \n";
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

                squadBoons[playerSquad].MightStacks +=          fightPhase?.BuffsStatContainer.BoonStats?[index].Data?.Count > 0  ? (float)(fightPhase.BuffsStatContainer.BoonStats?[index].Data?[0]?.FirstOrDefault() ?? 0.0f) : 0.0f;
                squadBoons[playerSquad].FuryPercent +=          fightPhase?.BuffsStatContainer.BoonStats?[index].Data?.Count > 1  ? (float)(fightPhase.BuffsStatContainer.BoonStats?[index].Data?[1]?.FirstOrDefault() ?? 0.0f) : 0.0f;
                squadBoons[playerSquad].QuickPercent +=         fightPhase?.BuffsStatContainer.BoonStats?[index].Data?.Count > 2  ? (float)(fightPhase.BuffsStatContainer.BoonStats?[index].Data?[2]?.FirstOrDefault() ?? 0.0f) : 0.0f;
                squadBoons[playerSquad].AlacrityPercent +=      fightPhase?.BuffsStatContainer.BoonStats?[index].Data?.Count > 3  ? (float)(fightPhase.BuffsStatContainer.BoonStats?[index].Data?[3]?.FirstOrDefault() ?? 0.0f) : 0.0f;
                squadBoons[playerSquad].ProtectionPercent +=    fightPhase?.BuffsStatContainer.BoonStats?[index].Data?.Count > 4  ? (float)(fightPhase.BuffsStatContainer.BoonStats?[index].Data?[4]?.FirstOrDefault() ?? 0.0f) : 0.0f;
                squadBoons[playerSquad].RegenPercent +=         fightPhase?.BuffsStatContainer.BoonStats?[index].Data?.Count > 5  ? (float)(fightPhase.BuffsStatContainer.BoonStats?[index].Data?[5]?.FirstOrDefault() ?? 0.0f) : 0.0f;
                squadBoons[playerSquad].VigorPercent +=         fightPhase?.BuffsStatContainer.BoonStats?[index].Data?.Count > 6  ? (float)(fightPhase.BuffsStatContainer.BoonStats?[index].Data?[6]?.FirstOrDefault() ?? 0.0f) : 0.0f;
                squadBoons[playerSquad].AegisPercent +=         fightPhase?.BuffsStatContainer.BoonStats?[index].Data?.Count > 7  ? (float)(fightPhase.BuffsStatContainer.BoonStats?[index].Data?[7]?.FirstOrDefault() ?? 0.0f) : 0.0f;
                squadBoons[playerSquad].StabilityPercent +=     fightPhase?.BuffsStatContainer.BoonStats?[index].Data?.Count > 8  ? (float)(fightPhase.BuffsStatContainer.BoonStats?[index].Data?[8]?.FirstOrDefault() ?? 0.0f) : 0.0f;
                squadBoons[playerSquad].SwiftnessPercent +=     fightPhase?.BuffsStatContainer.BoonStats?[index].Data?.Count > 9  ? (float)(fightPhase.BuffsStatContainer.BoonStats?[index].Data?[9]?.FirstOrDefault() ?? 0.0f) : 0.0f;
                squadBoons[playerSquad].ResistancePercent +=    fightPhase?.BuffsStatContainer.BoonStats?[index].Data?.Count > 10 ? (float)(fightPhase.BuffsStatContainer.BoonStats?[index].Data?[10]?.FirstOrDefault() ?? 0.0f) : 0.0f;
                squadBoons[playerSquad].ResolutionPercent +=    fightPhase?.BuffsStatContainer.BoonStats?[index].Data?.Count > 11 ? (float)(fightPhase.BuffsStatContainer.BoonStats?[index].Data?[11]?.FirstOrDefault() ?? 0.0f) : 0.0f;
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

        public Embed GenerateSimple(EliteInsightDataModel data, long guildId)
        {
            var fightPhase = data.Phases?.Any() ?? false
                ? data.Phases[0]
                : new ArcDpsPhase();

            var healingPhase = data.HealingStatsExtension?.HealingPhases?.FirstOrDefault() ?? new HealingPhase();
            var barrierPhase = data.BarrierStatsExtension?.BarrierPhases?.FirstOrDefault() ?? new BarrierPhase();

            var dateStartString = data.EncounterStart;
            var dateTimeStart = DateTime.ParseExact(dateStartString, "yyyy-MM-dd HH:mm:ss zzz", CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal);

            var duration = fightPhase.Duration;

            short encounterType;
            switch (data.EncounterId)
            {
                case 131329:
                    encounterType = (short)FightTypesEnum.Vale;
                    break;
                case 131330:
                    encounterType = (short)FightTypesEnum.Gorseval;
                    break;
                case 131331:
                    encounterType = (short)FightTypesEnum.Sabetha;
                    break;
                case 131585:
                    encounterType = (short)FightTypesEnum.Sloth;
                    break;
                case 131586:
                    encounterType = (short)FightTypesEnum.Trio;
                    break;
                case 131587:
                    encounterType = (short)FightTypesEnum.Matthias;
                    break;
                case 131841:
                    encounterType = (short)FightTypesEnum.Escort;
                    break;
                case 131842:
                    encounterType = (short)FightTypesEnum.KC;
                    break;
                case 131843:
                    encounterType = (short)FightTypesEnum.TC;
                    break;
                case 131844:
                    encounterType = (short)FightTypesEnum.Xera;
                    break;
                case 132097:
                    encounterType = (short)FightTypesEnum.Cairn;
                    break;
                case 132098:
                    encounterType = (short)FightTypesEnum.MO;
                    break;
                case 132099:
                    encounterType = (short)FightTypesEnum.Samarog;
                    break;
                case 132100:
                    encounterType = (short)FightTypesEnum.Deimos;
                    break;
                case 132353:
                    encounterType = (short)FightTypesEnum.SH;
                    break;
                case 132354:
                    encounterType = (short)FightTypesEnum.River;
                    break;
                case 132355:
                    encounterType = (short)FightTypesEnum.BK;
                    break;
                case 132356:
                    encounterType = (short)FightTypesEnum.EoS;
                    break;
                case 132357:
                    encounterType = (short)FightTypesEnum.SoD;
                    break;
                case 132358:
                    encounterType = (short)FightTypesEnum.Dhuum;
                    break;
                case 132609:
                    encounterType = (short)FightTypesEnum.CA;
                    break;
                case 132610:
                    encounterType = (short)FightTypesEnum.Largos;
                    break;
                case 132611:
                    encounterType = (short)FightTypesEnum.Qadim;
                    break;
                case 132865:
                    encounterType = (short)FightTypesEnum.Adina;
                    break;
                case 132866:
                    encounterType = (short)FightTypesEnum.Sabir;
                    break;
                case 132867:
                    encounterType = (short)FightTypesEnum.Peerless;
                    break;
                case 262913:
                    encounterType = (short)FightTypesEnum.AH;
                    break;
                case 262914:
                    encounterType = (short)FightTypesEnum.XJJ;
                    break;
                case 262915:
                    encounterType = (short)FightTypesEnum.KO;
                    break;
                case 262916:
                    encounterType = (short)FightTypesEnum.HT;
                    break;
                case 262917:
                    encounterType = (short)FightTypesEnum.OLC;
                    break;
                case 263425:
                    encounterType = (short)FightTypesEnum.CO;
                    break;
                case 263426:
                    encounterType = (short)FightTypesEnum.ToF;
                    break;
                case 196865:
                    encounterType = (short)FightTypesEnum.MAMA;
                    break;
                case 196866:
                    encounterType = (short)FightTypesEnum.Siax;
                    break;
                case 196867:
                    encounterType = (short)FightTypesEnum.Ensolyss;
                    break;
                case 197121:
                    encounterType = (short)FightTypesEnum.Skorvald;
                    break;
                case 197122:
                    encounterType = (short)FightTypesEnum.Artsariiv;
                    break;
                case 197123:
                    encounterType = (short)FightTypesEnum.Arkk;
                    break;
                case 197378:
                    encounterType = (short)FightTypesEnum.Ai_Ele;
                    break;
                case 197379:
                    encounterType = (short)FightTypesEnum.Ai_Dark;
                    break;
                case 197377:
                    encounterType = (short)FightTypesEnum.Ai_Both;
                    break;
                case 197633:
                    encounterType = (short)FightTypesEnum.Kanaxai;
                    break;
                default:
                    encounterType = (short)FightTypesEnum.unkn;
                    break;
            }

            var gw2Players = _playerService.GetGw2Players(data, fightPhase, healingPhase, barrierPhase, encounterType);
            var mainTarget = data.Targets?.FirstOrDefault() ?? new ArcDpsTarget
            {
                HpLeft = 1,
                Health = 1
            };

            var fightLog = _databaseContext.FightLog.FirstOrDefault(s => s.Url == data.Url);
            if (fightLog == null)
            {
                fightLog = new FightLog
                {
                    GuildId = guildId,
                    Url = data.Url ?? string.Empty,
                    FightType = encounterType,
                    FightStart = dateTimeStart,
                    FightDurationInMs = duration,
                    IsSuccess = data.Success,
                    FightPercent = Math.Round(((decimal)mainTarget.HpLeft / (decimal)mainTarget.Health) * 100, 2)
                };

                _databaseContext.Add(fightLog);
                _databaseContext.SaveChanges();

                var playerFights = gw2Players.Select(gw2Player => new PlayerFightLog
                {
                    FightLogId = fightLog.FightLogId,
                    GuildWarsAccountName = gw2Player.AccountName,
                    Damage = gw2Player.Damage,
                    Cleave = gw2Player.Cleave,
                    Kills = gw2Player.Kills,
                    Downs = gw2Player.Downs,
                    Deaths = gw2Player.Deaths,
                    QuicknessDuration = Convert.ToDecimal(gw2Player.TotalQuick),
                    AlacDuration = Convert.ToDecimal(gw2Player.TotalAlac),
                    SubGroup = gw2Player.SubGroup,
                    DamageDownContribution = gw2Player.DamageDownContribution,
                    Cleanses = Convert.ToInt64(gw2Player.Cleanses),
                    Strips = Convert.ToInt64(gw2Player.Strips),
                    StabGenOnGroup = Convert.ToDecimal(gw2Player.StabOnGroup),
                    StabGenOffGroup = Convert.ToDecimal(gw2Player.StabOffGroup),
                    Healing = gw2Player.Healing,
                    BarrierGenerated = gw2Player.BarrierGenerated,
                    DistanceFromTag = Convert.ToDecimal(gw2Player.DistanceFromTag),
                    TimesDowned = Convert.ToInt32(gw2Player.TimesDowned),
                    Interrupts = gw2Player.Interrupts,
                    NumberOfHitsWhileBlinded = gw2Player.NumberOfHitsWhileBlinded,
                    NumberOfMissesAgainst = Convert.ToInt64(gw2Player.NumberOfMissesAgainst),
                    NumberOfTimesBlockedAttack = Convert.ToInt64(gw2Player.NumberOfTimesBlockedAttack),
                    NumberOfTimesEnemyBlockedAttack = gw2Player.NumberOfTimesEnemyBlockedAttack,
                    NumberOfBoonsRipped = Convert.ToInt64(gw2Player.NumberOfBoonsRipped),
                    DamageTaken = Convert.ToInt64(gw2Player.DamageTaken),
                    BarrierMitigation = Convert.ToInt64(gw2Player.BarrierMitigation),
                    CerusOrbsCollected = gw2Player.CerusOrbsCollected,
                    CerusSpreadHitCount = gw2Player.CerusSpreadHitCount,
                    CerusPhaseOneDamage = Convert.ToDecimal(gw2Player.CerusPhaseOneDamage),
                    DeimosOilsTriggered = gw2Player.DeimosOilsTriggered
                })
                .ToList();

                _databaseContext.AddRange(playerFights);
                _databaseContext.SaveChanges();
            }

            var message = new EmbedBuilder
            {
                Title = $"Fight Recorded - {data.FightName}\n",
                Description = $"**Length:** {data.EncounterDuration}{(data.Success ? string.Empty : $" - {fightLog.FightPercent}%")}\n",
                Color = data.Success ? Color.Green : Color.Red,
                Author = new EmbedAuthorBuilder()
                {
                    Name = "GW2-DonBot",
                    Url = "https://github.com/LoganWal/GW2-DonBot",
                    IconUrl = "https://i.imgur.com/tQ4LD6H.png"
                },
                Url = $"{data.Url}",
                Footer = new EmbedFooterBuilder()
                {
                    Text = $"{_footerHandler.Generate()}",
                    IconUrl = "https://i.imgur.com/tQ4LD6H.png"
                },
                Timestamp = DateTime.Now
            };

            var fightInSeconds = (float)fightLog.FightDurationInMs / 1000f;
            var playerOverview = "```Player         Dmg       Cleave    Alac    Quick                                                         \n";
            foreach (var gw2Player in gw2Players.OrderByDescending(s => s.Damage))
            {
                playerOverview += $"{gw2Player.AccountName.ClipAt(13),-13}{string.Empty,2}{((float)gw2Player.Damage / (fightInSeconds)).FormatNumber(true),-8}{string.Empty,2}{((float)gw2Player.Cleave / (fightInSeconds)).FormatNumber(true),-8}{string.Empty,2}{Math.Round(gw2Player.TotalAlac, 2).ToString(CultureInfo.CurrentCulture),-5}{string.Empty,3}{Math.Round(gw2Player.TotalQuick, 2).ToString(CultureInfo.CurrentCulture),-5}\n";
            }

            playerOverview += "```";

            message.AddField(x =>
            {
                x.Name = "Player Overview";
                x.Value = $"{playerOverview}";
                x.IsInline = false;
            });

            var mechanicsOverview = string.Empty;

            if (encounterType == (short)FightTypesEnum.ToF)
            {
                mechanicsOverview = "```Player         P1 Dmg    Orbs   Downed\n";
                foreach (var gw2Player in gw2Players.OrderByDescending(s => s.CerusPhaseOneDamage)) 
                {
                    mechanicsOverview += $"{gw2Player.AccountName?.ClipAt(13),-13}{string.Empty,2}{((float)gw2Player.CerusPhaseOneDamage).FormatNumber(true),-8}{string.Empty,2}{gw2Player.CerusOrbsCollected,-3}{string.Empty,4}{gw2Player.TimesDowned,-3}\n";
                }

                mechanicsOverview += "```";
            }

            if (encounterType == (short)FightTypesEnum.Deimos)
            {
                mechanicsOverview = "```Player         Oils\n";
                foreach (var gw2Player in gw2Players.OrderByDescending(s => s.DeimosOilsTriggered))
                {
                    mechanicsOverview += $"{gw2Player.AccountName?.ClipAt(13),-13}{string.Empty,2}{gw2Player.DeimosOilsTriggered}\n";
                }

                mechanicsOverview += "```";
            }

            if (!string.IsNullOrEmpty(mechanicsOverview))
            {
                message.AddField(x =>
                {
                    x.Name = "Mechanics Overview";
                    x.Value = $"{mechanicsOverview}";
                    x.IsInline = false;
                });
            }
            
            // Building the message for use
            return message.Build();
        }
    }
}
