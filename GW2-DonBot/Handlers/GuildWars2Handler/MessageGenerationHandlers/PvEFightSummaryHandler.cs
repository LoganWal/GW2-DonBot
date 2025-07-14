using Discord;
using DonBot.Extensions;
using DonBot.Models.Entities;
using DonBot.Models.Enums;
using DonBot.Models.GuildWars2;
using DonBot.Services.DatabaseServices;
using DonBot.Services.GuildWarsServices;
using System.Globalization;

namespace DonBot.Handlers.GuildWars2Handler.MessageGenerationHandlers;

public class PvEFightSummaryHandler(
    IEntityService entityService,
    FooterHandler footerHandler,
    IPlayerService playerService)
{
    public async Task<Embed> GenerateSimple(EliteInsightDataModel data, long guildId)
    {
        var fightPhase = data.FightEliteInsightDataModel.Phases?.Any() ?? false
            ? data.FightEliteInsightDataModel.Phases[0]
            : new ArcDpsPhase();

        var dateStartString = data.FightEliteInsightDataModel.EncounterStart;
        var dateTimeStart = DateTime.ParseExact(dateStartString, "yyyy-MM-dd HH:mm:ss zzz", CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal);

        var duration = fightPhase.Duration;
        var sumAllTargets = true;

        short encounterType;
        switch (data.FightEliteInsightDataModel.EncounterId)
        {
            case 131329:
                encounterType = (short)FightTypesEnum.Vale;
                sumAllTargets = false;
                break;
            case 131332:
                encounterType = (short)FightTypesEnum.Spirit;
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
            case 133121:
                encounterType = (short)FightTypesEnum.Greer;
                sumAllTargets = false;
                break;
            case 133122:
                encounterType = (short)FightTypesEnum.Decima;
                sumAllTargets = false;
                break;
            case 133123:
                encounterType = (short)FightTypesEnum.Ura;
                sumAllTargets = false;
                break;
            case 262657:
                encounterType = (short)FightTypesEnum.Icebrood;
                break;
            case 262658:
                encounterType = (short)FightTypesEnum.Fraenir;
                break;
            case 262659:
                encounterType = (short)FightTypesEnum.Kodan;
                break;
            case 262661:
                encounterType = (short)FightTypesEnum.Whisper;
                break;
            case 262660:
                encounterType = (short)FightTypesEnum.Boneskinner;
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
            case 197890:
                encounterType = (short)FightTypesEnum.Eparch;
                break;
            default:
                encounterType = (short)FightTypesEnum.Unkn;
                break;
        }

        var gw2Players = playerService.GetGw2Players(data, fightPhase, encounterType, sumAllTargets);
        var mainTarget = data.FightEliteInsightDataModel.Targets?.FirstOrDefault() ?? new ArcDpsTarget
        {
            HpLeft = 1,
            Health = 1
        };

        var existingFightLog = await entityService.FightLog.GetFirstOrDefaultAsync(s => s.Url == data.FightEliteInsightDataModel.Url);

        if (existingFightLog != null)
        {
            // Update the properties of the existing fight log
            existingFightLog.GuildId = guildId;
            existingFightLog.FightType = encounterType;
            existingFightLog.FightStart = dateTimeStart;
            existingFightLog.FightDurationInMs = duration;
            existingFightLog.IsSuccess = data.FightEliteInsightDataModel.Success;
            existingFightLog.FightPercent = Math.Round((mainTarget.HpLeft / (decimal)mainTarget.Health) * 100, 2);
            existingFightLog.FightMode = !string.IsNullOrEmpty(data.FightEliteInsightDataModel.FightMode)
                ? data.FightEliteInsightDataModel.GetFightMode()
                : data.FightEliteInsightDataModel.FightName?.Split(' ').LastOrDefault() switch
                {
                    "CM" => 1,
                    "LCM" => 2,
                    _ => 0
                };

            if (encounterType == (short)FightTypesEnum.Ht)
            {
                var finalTarget = data.FightEliteInsightDataModel.Targets?.LastOrDefault(s => s.HbWidth == 800) ?? mainTarget;
                existingFightLog.FightPhase = data.FightEliteInsightDataModel.Targets?.Count(s => s.HbWidth == 800);
                existingFightLog.FightPercent = Math.Round((finalTarget.HpLeft / (decimal)finalTarget.Health) * 100, 2);
            }

            await entityService.FightLog.UpdateAsync(existingFightLog);
        }
        else
        {
            // Create a new fight log
            var fightLog = new FightLog
            {
                GuildId = guildId,
                Url = data.FightEliteInsightDataModel.Url,
                FightType = encounterType,
                FightStart = dateTimeStart,
                FightDurationInMs = duration,
                IsSuccess = data.FightEliteInsightDataModel.Success,
                FightPercent = Math.Round((mainTarget.HpLeft / (decimal)mainTarget.Health) * 100, 2),
                FightMode = !string.IsNullOrEmpty(data.FightEliteInsightDataModel.FightMode)
                    ? data.FightEliteInsightDataModel.GetFightMode()
                    : data.FightEliteInsightDataModel.FightName?.Split(' ').LastOrDefault() switch
                    {
                        "CM" => 1,
                        "LCM" => 2,
                        _ => 0
                    }
            };

            if (encounterType == (short)FightTypesEnum.Ht)
            {
                var finalTarget = data.FightEliteInsightDataModel.Targets?.LastOrDefault(s => s.HbWidth == 800) ?? mainTarget;
                fightLog.FightPhase = data.FightEliteInsightDataModel.Targets?.Count(s => s.HbWidth == 800);
                fightLog.FightPercent = Math.Round((finalTarget.HpLeft / (decimal)finalTarget.Health) * 100, 2);
            }

            await entityService.FightLog.AddAsync(fightLog);

            // Add player fight logs
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
                ResurrectionTime = gw2Player.ResurrectionTime,
                FavorUsage = gw2Player.FavorUsage,
                DesertShroudUsage = gw2Player.DesertShroudUsage,
                SandstormShroudUsage = gw2Player.SandstormShroudUsage,
                SandFlareUsage = gw2Player.SandFlareUsage,
                Exposed = gw2Player.Exposed,
                ShardPickUp = gw2Player.ShardPickUp,
                ShardUsed = gw2Player.ShardUsed
            }).ToList();

            await entityService.PlayerFightLog.AddRangeAsync(playerFights);
        }

        var message = new EmbedBuilder
        {
            Title = $"{data.FightEliteInsightDataModel.FightName}\n",
            Description = $"**Length:** {data.FightEliteInsightDataModel.EncounterDuration}{(data.FightEliteInsightDataModel.Success ? string.Empty : $" {(existingFightLog?.FightPhase != null ? (existingFightLog.FightPhase) : string.Empty)} - {existingFightLog?.FightPercent}%")}\n",
            Color = data.FightEliteInsightDataModel.Success ? Color.Green : Color.Red,
            Author = new EmbedAuthorBuilder()
            {
                Name = "GW2-DonBot",
                Url = "https://github.com/LoganWal/GW2-DonBot",
                IconUrl = "https://i.imgur.com/tQ4LD6H.png"
            },
            Url = $"{data.FightEliteInsightDataModel.Url}",
            Footer = new EmbedFooterBuilder()
            {
                Text = $"{await footerHandler.Generate(guildId)}",
                IconUrl = "https://i.imgur.com/tQ4LD6H.png"
            },
            Timestamp = DateTime.Now
        };

        var fightInSeconds = existingFightLog?.FightDurationInMs / 1000f ?? duration / 1000f;
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

        if (encounterType == (short)FightTypesEnum.Ura)
        {
            mechanicsOverview = "```Player         Exposed    Shard P    Shard U\n";
            foreach (var gw2Player in gw2Players.OrderByDescending(s => s.Exposed))
            {
                mechanicsOverview += $"{gw2Player.AccountName.ClipAt(13),-13}{string.Empty,2}{gw2Player.Exposed,-3}{string.Empty,8}{gw2Player.ShardPickUp,-3}{string.Empty,8}{gw2Player.ShardUsed,-3}\n";
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