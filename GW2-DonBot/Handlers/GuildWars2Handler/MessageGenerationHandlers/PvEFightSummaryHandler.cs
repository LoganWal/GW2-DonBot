using Discord;
using DonBot.Extensions;
using DonBot.Models.Entities;
using DonBot.Models.Enums;
using DonBot.Models.GuildWars2;
using DonBot.Services.DatabaseServices;
using DonBot.Services.GuildWarsServices;
using System.Globalization;

namespace DonBot.Handlers.GuildWars2Handler.MessageGenerationHandlers
{
    public class PvEFightSummaryHandler(
        IEntityService entityService,
        FooterHandler footerHandler,
        IPlayerService playerService)
    {
        public async Task<Embed> GenerateSimple(EliteInsightDataModel data, long guildId)
        {
            var fightPhase = data.Phases?.Any() ?? false
                ? data.Phases[0]
                : new ArcDpsPhase();

            var healingPhase = data.HealingStatsExtension?.HealingPhases?.FirstOrDefault() ?? new HealingPhase();
            var barrierPhase = data.BarrierStatsExtension?.BarrierPhases?.FirstOrDefault() ?? new BarrierPhase();

            var dateStartString = data.EncounterStart;
            var dateTimeStart = DateTime.ParseExact(dateStartString, "yyyy-MM-dd HH:mm:ss zzz", CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal);

            var duration = fightPhase.Duration;
            var sumAllTargets = true;

            short encounterType;
            switch (data.EncounterId)
            {
                case 131329:
                    encounterType = (short)FightTypesEnum.Vale;
                    sumAllTargets = false;
                    break;
                case 131330:
                    encounterType = (short)FightTypesEnum.Gorseval;
                    sumAllTargets = false;
                    break;
                case 131331:
                    encounterType = (short)FightTypesEnum.Sabetha;
                    sumAllTargets = false;
                    break;
                case 131585:
                    encounterType = (short)FightTypesEnum.Sloth;
                    sumAllTargets = false;
                    break;
                case 131586:
                    encounterType = (short)FightTypesEnum.Trio;
                    sumAllTargets = false;
                    break;
                case 131587:
                    encounterType = (short)FightTypesEnum.Matthias;
                    sumAllTargets = false;
                    break;
                case 131841:
                    encounterType = (short)FightTypesEnum.Escort;
                    sumAllTargets = false;
                    break;
                case 131842:
                    encounterType = (short)FightTypesEnum.Kc;
                    sumAllTargets = false;
                    break;
                case 131843:
                    encounterType = (short)FightTypesEnum.Tc;
                    sumAllTargets = false;
                    break;
                case 131844:
                    encounterType = (short)FightTypesEnum.Xera;
                    sumAllTargets = false;
                    break;
                case 132097:
                    encounterType = (short)FightTypesEnum.Cairn;
                    sumAllTargets = false;
                    break;
                case 132098:
                    encounterType = (short)FightTypesEnum.Mo;
                    sumAllTargets = false;
                    break;
                case 132099:
                    encounterType = (short)FightTypesEnum.Samarog;
                    sumAllTargets = false;
                    break;
                case 132100:
                    encounterType = (short)FightTypesEnum.Deimos;
                    sumAllTargets = false;
                    break;
                case 132353:
                    encounterType = (short)FightTypesEnum.Sh;
                    sumAllTargets = false;
                    break;
                case 132354:
                    encounterType = (short)FightTypesEnum.River;
                    sumAllTargets = false;
                    break;
                case 132355:
                    encounterType = (short)FightTypesEnum.Bk;
                    sumAllTargets = false;
                    break;
                case 132356:
                    encounterType = (short)FightTypesEnum.EoS;
                    sumAllTargets = false;
                    break;
                case 132357:
                    encounterType = (short)FightTypesEnum.SoD;
                    sumAllTargets = false;
                    break;
                case 132358:
                    encounterType = (short)FightTypesEnum.Dhuum;
                    sumAllTargets = false;
                    break;
                case 132609:
                    encounterType = (short)FightTypesEnum.Ca;
                    sumAllTargets = false;
                    break;
                case 132610:
                    encounterType = (short)FightTypesEnum.Largos;
                    break;
                case 132611:
                    encounterType = (short)FightTypesEnum.Qadim;
                    sumAllTargets = false;
                    break;
                case 132865:
                    encounterType = (short)FightTypesEnum.Adina;
                    sumAllTargets = false;
                    break;
                case 132866:
                    encounterType = (short)FightTypesEnum.Sabir;
                    sumAllTargets = false;
                    break;
                case 132867:
                    encounterType = (short)FightTypesEnum.Peerless;
                    sumAllTargets = false;
                    break;
                case 262913:
                    encounterType = (short)FightTypesEnum.Ah;
                    break;
                case 262914:
                    encounterType = (short)FightTypesEnum.Xjj;
                    break;
                case 262915:
                    encounterType = (short)FightTypesEnum.Ko;
                    break;
                case 262916:
                    encounterType = (short)FightTypesEnum.Ht;
                    break;
                case 262917:
                    encounterType = (short)FightTypesEnum.Olc;
                    break;
                case 263425:
                    encounterType = (short)FightTypesEnum.Co;
                    break;
                case 263426:
                    encounterType = (short)FightTypesEnum.ToF;
                    break;
                case 196865:
                    encounterType = (short)FightTypesEnum.Mama;
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
                    encounterType = (short)FightTypesEnum.AiEle;
                    break;
                case 197379:
                    encounterType = (short)FightTypesEnum.AiDark;
                    break;
                case 197377:
                    encounterType = (short)FightTypesEnum.AiBoth;
                    break;
                case 197633:
                    encounterType = (short)FightTypesEnum.Kanaxai;
                    break;
                default:
                    encounterType = (short)FightTypesEnum.Unkn;
                    break;
            }

            var gw2Players = playerService.GetGw2Players(data, fightPhase, healingPhase, barrierPhase, encounterType, sumAllTargets);
            var mainTarget = data.Targets?.FirstOrDefault() ?? new ArcDpsTarget
            {
                HpLeft = 1,
                Health = 1
            };

            var fightLog = await entityService.FightLog.GetFirstOrDefaultAsync(s => s.Url == data.Url);
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
                    FightPercent = Math.Round((mainTarget.HpLeft / (decimal)mainTarget.Health) * 100, 2)
                };

                if (encounterType == (short)FightTypesEnum.Ht)
                {
                    var finalTarget = data.Targets?.LastOrDefault(s => s.HbWidth == 800) ?? mainTarget;
                    fightLog.FightPhase = data.Targets?.Count(s => s.HbWidth == 800);
                    fightLog.FightPercent = Math.Round((finalTarget.HpLeft / (decimal)finalTarget.Health) * 100, 2);
                }

                await entityService.FightLog.AddAsync(fightLog);

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
                    DeimosOilsTriggered = gw2Player.DeimosOilsTriggered,
                    TimesInterrupted = gw2Player.TimesInterrupted,
                    ResurrectionTime = gw2Player.ResurrectionTime
                })
                .ToList();

                await entityService.PlayerFightLog.AddRangeAsync(playerFights);
            }

            var message = new EmbedBuilder
            {
                Title = $"Fight Recorded - {data.FightName}\n",
                Description = $"**Length:** {data.EncounterDuration}{(data.Success ? string.Empty : $" {(fightLog.FightPhase != null ? (fightLog.FightPhase) : string.Empty)} - {fightLog.FightPercent}%")}\n",
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
                    Text = $"{await footerHandler.Generate(guildId)}",
                    IconUrl = "https://i.imgur.com/tQ4LD6H.png"
                },
                Timestamp = DateTime.Now
            };

            var fightInSeconds = fightLog.FightDurationInMs / 1000f;
            var playerOverview = "```Player         Dmg       Cleave    Alac    Quick                                                         \n";
            foreach (var gw2Player in gw2Players.OrderByDescending(s => s.Damage))
            {
                playerOverview += $"{gw2Player.AccountName.ClipAt(13),-13}{string.Empty,2}{(gw2Player.Damage / (fightInSeconds)).FormatNumber(true),-8}{string.Empty,2}{(gw2Player.Cleave / (fightInSeconds)).FormatNumber(true),-8}{string.Empty,2}{Math.Round(gw2Player.TotalAlac, 2).ToString(CultureInfo.CurrentCulture),-5}{string.Empty,3}{Math.Round(gw2Player.TotalQuick, 2).ToString(CultureInfo.CurrentCulture),-5}\n";
            }

            playerOverview += "```";

            message.AddField(x =>
            {
                x.Name = "Player Overview";
                x.Value = $"{playerOverview}";
                x.IsInline = false;
            });

            var survivabilityOverview = "```Player         Res (s)    Dmg Taken   Times Downed                                      \n";
            foreach (var gw2Player in gw2Players.OrderBy(s => s.DamageTaken))
            {
                survivabilityOverview += $"{gw2Player.AccountName.ClipAt(13),-13}{string.Empty,2}{Math.Round((double)gw2Player.ResurrectionTime / 1000, 3),-9}{string.Empty,2}{(gw2Player.DamageTaken),-10}{string.Empty,2}{gw2Player.TimesDowned}\n";
            }

            survivabilityOverview += "```";

            message.AddField(x =>
            {
                x.Name = "Survivability Overview";
                x.Value = $"{survivabilityOverview}";
                x.IsInline = false;
            });

            var mechanicsOverview = string.Empty;

            if (encounterType == (short)FightTypesEnum.ToF)
            {
                mechanicsOverview = "```Player         P1 Dmg    Orbs\n";
                foreach (var gw2Player in gw2Players.OrderByDescending(s => s.CerusPhaseOneDamage)) 
                {
                    mechanicsOverview += $"{gw2Player.AccountName.ClipAt(13),-13}{string.Empty,2}{((float)gw2Player.CerusPhaseOneDamage).FormatNumber(true),-8}{string.Empty,2}{gw2Player.CerusOrbsCollected,-3}\n";
                }

                mechanicsOverview += "```";
            }

            if (encounterType == (short)FightTypesEnum.Deimos)
            {
                mechanicsOverview = "```Player         Oils\n";
                foreach (var gw2Player in gw2Players.OrderByDescending(s => s.DeimosOilsTriggered))
                {
                    mechanicsOverview += $"{gw2Player.AccountName.ClipAt(13),-13}{string.Empty,2}{gw2Player.DeimosOilsTriggered}\n";
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
