using System.Globalization;
using Discord;
using Discord.WebSocket;
using DonBot.Extensions;
using DonBot.Models.Entities;
using DonBot.Models.Enums;
using DonBot.Models.GuildWars2;
using DonBot.Models.Statics;
using DonBot.Services.DatabaseServices;
using DonBot.Services.GuildWarsServices;

namespace DonBot.Handlers.GuildWars2Handler.MessageGenerationHandlers;

public class WvWFightSummaryHandler(
    IEntityService entityService,
    IPlayerService playerService,
    FooterHandler footerHandler)
{
    public async Task<Embed> Generate(EliteInsightDataModel data, bool advancedLog, Guild guild, DiscordSocketClient client)
    {
        var playerCount = 5;

        // Building the actual message to be sent
        var logLength = data.FightEliteInsightDataModel.EncounterDuration?.TimeToSeconds() ?? 0;

        var friendlyCount = data.FightEliteInsightDataModel.Players?.Count ?? 0;
        var squadMemberCount = data.FightEliteInsightDataModel.Players?.Count(s => !s.NotInSquad) ?? 0;

        // remove one from target dummy
        var enemyCount = (data.FightEliteInsightDataModel.Targets?.Count - 1) ?? 0;
        var enemyDamage = data.FightEliteInsightDataModel.Targets?
            .Sum(player => player.Details?.DmgDistributions?.Any() ?? false
                ? player.Details?.DmgDistributions[0].ContributedDamage
                : 0) ?? 0;

        var enemyDps = enemyDamage / logLength;

        var fightPhase = data.FightEliteInsightDataModel.Phases?.Any() ?? false
            ? data.FightEliteInsightDataModel.Phases[0]
            : new ArcDpsPhase();

        var gw2Players = playerService.GetGw2Players(data, fightPhase);

        var friendlyDamage = gw2Players.Sum(s => s.Damage);
        var friendlyDps = friendlyDamage / logLength;

        var friendlyCountStr = $"{friendlyCount}({squadMemberCount})".PadCenter(7);
        var friendlyDamageStr = friendlyDamage.FormatNumber().PadCenter(7);
        var friendlyDpsStr = friendlyDps.FormatNumber().PadCenter(7);
        var friendlyDownsStr = gw2Players.Sum(s => s.TimesDowned).ToString(CultureInfo.CurrentCulture).PadCenter(7);
        var friendlyDeathsStr = gw2Players.Sum(s => s.Deaths).ToString().PadCenter(7);

        var enemyCountStr = enemyCount.ToString().PadCenter(7);
        var enemyDamageStr = enemyDamage.FormatNumber().PadCenter(7);
        var enemyDpsStr = enemyDps.FormatNumber().PadCenter(7);
        var enemyDownsStr = gw2Players.Sum(s => s.Downs).ToString().PadCenter(7);
        var enemyDeathsStr = gw2Players.Sum(s => s.Kills).ToString().PadCenter(7);

        if (!advancedLog && guild.StreamLogChannelId.HasValue)
        {
            var streamMessage =
                $@"```
Who     Count    Damage      DPS       Downs     Deaths  
Friends {friendlyCountStr.Trim(),-7} {friendlyDamageStr.Trim(),-7}     {friendlyDpsStr.Trim(),-6}    {friendlyDownsStr.Trim(),-3}       {friendlyDeathsStr.Trim(),-3}   
Enemies {enemyCountStr.Trim(),-3}      {enemyDamageStr.Trim(),-7}     {enemyDpsStr.Trim(),-6}    {enemyDownsStr.Trim(),-3}       {enemyDeathsStr.Trim(),-3}
```";

            if (client.GetChannel((ulong)guild.StreamLogChannelId) is ITextChannel streamLogChannel)
            {
                await streamLogChannel.SendMessageAsync(text: streamMessage);
            }
        }

        // Battleground parsing
        var range = (int)MathF.Min(15, data.FightEliteInsightDataModel.FightName?.Length - 1 ?? 0)..;
        var rangeStart = range.Start.GetOffset(data.FightEliteInsightDataModel.FightName?.Length ?? 0);
        var rangeEnd = range.End.GetOffset(data.FightEliteInsightDataModel.FightName?.Length ?? 0);

        if (rangeStart < 0 || rangeStart > data.FightEliteInsightDataModel.FightName?.Length || rangeEnd < 0 || rangeEnd > data.FightEliteInsightDataModel.FightName?.Length)
        {
            throw new Exception($"Bad battleground name: {data.FightEliteInsightDataModel.FightName}");
        }

        var battleGround = data.FightEliteInsightDataModel.FightName?[range] ?? string.Empty;

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
        var friendlyOverview = "```Who   Count   DMG      DPS     Downs   Deaths         \n";
        friendlyOverview += $"Ally  {friendlyCountStr.Trim(),-7}{string.Empty,1}{friendlyDamageStr.Trim(),-7}{string.Empty,2}{friendlyDpsStr.Trim(),-6}{string.Empty,2}{friendlyDownsStr.Trim(),-3}{string.Empty,5}{friendlyDeathsStr.Trim(),-3}\n";
        friendlyOverview += $"Foe   {enemyCountStr.Trim(),-3}{string.Empty,5}{enemyDamageStr.Trim(),-7}{string.Empty,2}{enemyDpsStr.Trim(),-6}{string.Empty,2}{enemyDownsStr.Trim(),-3}{string.Empty,5}{enemyDeathsStr.Trim(),-3}```";

        var dateStartString = data.FightEliteInsightDataModel.Start;    
        var dateTimeStart = DateTime.ParseExact(dateStartString, "yyyy-MM-dd HH:mm:ss zzz", CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal);

        var dateEndString = data.FightEliteInsightDataModel.End;
        var dateTimeEnd = DateTime.ParseExact(dateEndString, "yyyy-MM-dd HH:mm:ss zzz", CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal);

        var duration = dateTimeEnd - dateTimeStart;

        if (!advancedLog)
        {
            var fightLog = await entityService.FightLog.GetFirstOrDefaultAsync(s => s.Url == data.FightEliteInsightDataModel.Url);

            if (fightLog == null)
            {
                fightLog = new FightLog
                {
                    GuildId = guild.GuildId,
                    Url = data.FightEliteInsightDataModel.Url ?? string.Empty,
                    FightType = (short)FightTypesEnum.WvW,
                    FightStart = dateTimeStart,
                    FightDurationInMs = (long)duration.TotalMilliseconds,
                    IsSuccess = data.FightEliteInsightDataModel.Success ?? fightPhase?.Success ?? false
                };

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
                        ResurrectionTime = gw2Player.ResurrectionTime,
                        FavorUsage = gw2Player.FavorUsage
                    })
                    .ToList();

                await entityService.PlayerFightLog.AddRangeAsync(playerFights);
            }
        }

        // Building the message via embeds
        var message = new EmbedBuilder
        {
            Title = $"{battleGroundEmoji} Report (WvW) - {battleGround}\n",
            Description = $"**Fight Duration:** {data.FightEliteInsightDataModel.EncounterDuration}\n",
            Color = (Color)battleGroundColor,
            Author = new EmbedAuthorBuilder()
            {
                Name = "GW2-DonBot",
                Url = "https://github.com/LoganWal/GW2-DonBot",
                IconUrl = "https://i.imgur.com/tQ4LD6H.png"
            },
            Url = $"{data.FightEliteInsightDataModel.Url}"
        };

        message.AddField(x =>
        {
            x.Name = "Friendly Overview";
            x.Value = $"{friendlyOverview}";
            x.IsInline = false;
        });

        return await GenerateMessage(advancedLog, playerCount, gw2Players, message, guild.GuildId);
    }

    public async Task<Embed> GenerateMessage(bool advancedLog, int playerCount, List<Gw2Player> gw2Players, EmbedBuilder message, long guildId, StatTotals? statTotals = null)
    {
        // Damage overview
        var damageOverview = "```#    Name                   Damage    Down C\n";

        var maxDamage = -1.0f;
        var maxDownContribution = -1.0f;
        var topDamage = gw2Players.OrderByDescending(s => s.Damage).Take(playerCount).ToList();
        var damageIndex = 1;
        foreach (var gw2Player in topDamage)
        {
            var name = !string.IsNullOrEmpty(gw2Player.CharacterName) ? gw2Player.CharacterName : gw2Player.AccountName;
            var prof = gw2Player.Profession;
            var damageFloat = (float)gw2Player.Damage;
            if (maxDamage <= 0.0f)
            {
                maxDamage = damageFloat;
            }

            var downContribution = (float)gw2Player.DamageDownContribution;
            if (maxDownContribution <= 0.0f)
            {
                maxDownContribution = downContribution;
            }

            damageOverview += $"{damageIndex.ToString().PadLeft(2, '0')}{string.Empty,3}{(name + EliteInsightExtensions.GetClassAppend(prof)).ClipAt(ArcDpsDataIndices.NameSizeLength),-ArcDpsDataIndices.NameSizeLength}{string.Empty,2}{damageFloat.FormatNumber(maxDamage),-8}{string.Empty,2}{downContribution.FormatNumber(maxDownContribution),-7}\n";
            damageIndex++;
        }

        damageOverview += "```";

        // Cleanse overview
        var cleanseOverview = $"```#    Name                   Cleanses\n";

        var topCleanses = gw2Players.OrderByDescending(s => s.Cleanses).Take(playerCount).ToList();
        var cleanseIndex = 1;
        foreach (var gw2Player in topCleanses)
        {
            var cleanses = gw2Player.Cleanses;
            var name = !string.IsNullOrEmpty(gw2Player.CharacterName) ? gw2Player.CharacterName : gw2Player.AccountName;
            var prof = gw2Player.Profession;

            cleanseOverview += $"{cleanseIndex.ToString().PadLeft(2, '0')}{string.Empty,3}{(name + EliteInsightExtensions.GetClassAppend(prof)).ClipAt(ArcDpsDataIndices.NameSizeLength),-ArcDpsDataIndices.NameSizeLength}{string.Empty,2}{cleanses.ToString(CultureInfo.InvariantCulture),-5}\n";
            cleanseIndex++;
        }

        cleanseOverview += "```";

        // Strip overview
        var stripOverview = "```#    Name                   Strips\n";

        var topStrips = gw2Players.OrderByDescending(s => s.Strips).Take(playerCount).ToList();
        var stripIndex = 1;
        foreach (var gw2Player in topStrips)
        {
            var strips = gw2Player.Strips;
            var name = !string.IsNullOrEmpty(gw2Player.CharacterName) ? gw2Player.CharacterName : gw2Player.AccountName;
            var prof = gw2Player.Profession;

            stripOverview += $"{stripIndex.ToString().PadLeft(2, '0')}{string.Empty,3}{(name + EliteInsightExtensions.GetClassAppend(prof)).ClipAt(ArcDpsDataIndices.NameSizeLength),-ArcDpsDataIndices.NameSizeLength}{string.Empty,2}{strips.ToString(CultureInfo.InvariantCulture),-5}\n";
            stripIndex++;
        }

        stripOverview += "```";

        // Stab overview
        var stabOverview = "```#    Name                   Sub  S(on)  S(off)                                                                 \n";

        var topStabs = gw2Players.OrderByDescending(s => s.StabOnGroup).Take(playerCount).ToList();
        var stabIndex = 1;
        foreach (var gw2Player in topStabs)
        {
            var stabOnGroup = gw2Player.StabOnGroup;
            var stabOffGroup = gw2Player.StabOffGroup;

            var sub = gw2Player.SubGroup;
            var name = !string.IsNullOrEmpty(gw2Player.CharacterName) ? gw2Player.CharacterName : gw2Player.AccountName;
            var prof = gw2Player.Profession;

            stabOverview += $"{stabIndex.ToString().PadLeft(2, '0')}{string.Empty,3}{(name + EliteInsightExtensions.GetClassAppend(prof)).ClipAt(ArcDpsDataIndices.NameSizeLength),-ArcDpsDataIndices.NameSizeLength}{string.Empty,2}{sub,-3}{string.Empty,2}{(Math.Round(stabOnGroup, 2)),-5}{string.Empty,2}{(Math.Round(stabOffGroup, 2)),-5}\n";
            stabIndex++;
        }

        stabOverview += "```";

        var healingOverview = "```#    Name                   Healing\n";

        var topHealing = gw2Players.OrderByDescending(s => s.Healing).Take(playerCount).ToList();
        var healingIndex = 1;
        foreach (var gw2Player in topHealing)
        {
            var healing = gw2Player.Healing;
            var name = !string.IsNullOrEmpty(gw2Player.CharacterName) ? gw2Player.CharacterName : gw2Player.AccountName;
            var prof = gw2Player.Profession;

            healingOverview += $"{healingIndex.ToString().PadLeft(2, '0')}{string.Empty,3}{(name + EliteInsightExtensions.GetClassAppend(prof)).ClipAt(ArcDpsDataIndices.NameSizeLength),-ArcDpsDataIndices.NameSizeLength}{string.Empty,2}{healing.FormatNumber().ToString(CultureInfo.InvariantCulture),-16}\n";
            healingIndex++;
        }

        healingOverview += "```";

        var distanceOverview = "```#    Name                    Distance From Tag                                                                    \n";
        var timesDownedOverview = "```#    Name                   Times Downed\n";
        var barrierOverview = "```#    Name                   Barrier Gen\n";
        var aggregations = "```Attacks Missed         Ours          Theirs                                                    \n";

        if (advancedLog)
        {
            var topBarrier = gw2Players.OrderByDescending(s => s.BarrierGenerated).Take(playerCount).ToList();
            var barrierIndex = 1;
            foreach (var gw2Player in topBarrier)
            {
                var barrier = gw2Player.BarrierGenerated;
                var name = !string.IsNullOrEmpty(gw2Player.CharacterName) ? gw2Player.CharacterName : gw2Player.AccountName;
                var prof = gw2Player.Profession;

                barrierOverview += $"{barrierIndex.ToString().PadLeft(2, '0')}{string.Empty,3}{(name + EliteInsightExtensions.GetClassAppend(prof)).ClipAt(ArcDpsDataIndices.NameSizeLength),-ArcDpsDataIndices.NameSizeLength}{string.Empty,2}{barrier.FormatNumber().ToString(CultureInfo.InvariantCulture)}\n";
                barrierIndex++;
            }
            barrierOverview += "```";

            var topDistance = gw2Players.OrderByDescending(s => s.DistanceFromTag).Take(playerCount).ToList();
            var distanceIndex = 1;
            foreach (var gw2Player in topDistance)
            {
                var distance = gw2Player.DistanceFromTag;
                var name = !string.IsNullOrEmpty(gw2Player.CharacterName) ? gw2Player.CharacterName : gw2Player.AccountName;
                var prof = gw2Player.Profession;

                distanceOverview += $"{distanceIndex.ToString().PadLeft(2, '0')}{string.Empty,3}{(name + EliteInsightExtensions.GetClassAppend(prof)).ClipAt(ArcDpsDataIndices.NameSizeLength),-ArcDpsDataIndices.NameSizeLength}{string.Empty,3}{distance.ToString(CultureInfo.InvariantCulture)}\n";
                distanceIndex++;
            }
            distanceOverview += "```";

            var topTimesDowned = gw2Players.OrderByDescending(s => s.TimesDowned).Take(playerCount).ToList();
            var timesDownedIndex = 1;
            foreach (var gw2Player in topTimesDowned)
            {
                var timesDowned = gw2Player.TimesDowned;
                var name = !string.IsNullOrEmpty(gw2Player.CharacterName) ? gw2Player.CharacterName : gw2Player.AccountName;
                var prof = gw2Player.Profession;

                timesDownedOverview += $"{timesDownedIndex.ToString().PadLeft(2, '0')}{string.Empty,3}{(name + EliteInsightExtensions.GetClassAppend(prof)).ClipAt(ArcDpsDataIndices.NameSizeLength),-ArcDpsDataIndices.NameSizeLength}{string.Empty,2}{timesDowned.ToString(CultureInfo.InvariantCulture)}\n";
                timesDownedIndex++;
            }

            timesDownedOverview += "```";

            aggregations += $"{string.Empty,23}{gw2Players.Sum(s => s.NumberOfHitsWhileBlinded),-4}{string.Empty,10}{gw2Players.Sum(s => s.NumberOfMissesAgainst).ToString(CultureInfo.CurrentCulture)}";
            aggregations += "```";
            aggregations += "```Attacks Blocked        Ours          Theirs                                          \n";
            aggregations += $"{string.Empty,23}{gw2Players.Sum(s => s.NumberOfTimesBlockedAttack).ToString(CultureInfo.CurrentCulture),-5}{string.Empty, 9}{gw2Players.Sum(s => s.NumberOfTimesEnemyBlockedAttack)}";
            aggregations += "```";

            aggregations += "```Boons Stripped         Ours          Theirs                                          \n";
            aggregations += $"{string.Empty,23}{(statTotals?.TotalStrips ?? gw2Players.Sum(s => s.Strips)).ToString(CultureInfo.CurrentCulture),-5}{string.Empty,9}{gw2Players.Sum(s => s.NumberOfBoonsRipped).ToString(CultureInfo.CurrentCulture)}";
            aggregations += "```";

            var totalDmg = Convert.ToSingle(gw2Players.Sum(s => s.DamageTaken));
            var totalBarrierMitigation = Convert.ToSingle(gw2Players.Sum(s => s.BarrierMitigation));

            aggregations += "```Damage Taken    Barrier Mit   Diff                                                   \n";
            aggregations += $"{totalDmg.FormatNumber(totalDmg).ToString(CultureInfo.CurrentCulture),-6}{string.Empty,10}{totalBarrierMitigation.FormatNumber(totalBarrierMitigation).ToString(CultureInfo.CurrentCulture),-6}{string.Empty,8}{(totalDmg - totalBarrierMitigation).FormatNumber(totalDmg - totalBarrierMitigation).ToString(CultureInfo.CurrentCulture)}({Math.Round((totalBarrierMitigation / totalDmg) * 100, 2)}%)";
            aggregations += "```";
        }

        if (!advancedLog)
        {
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
                x.Name = "Stab";
                x.Value = $"{stabOverview}";
                x.IsInline = false;
            });

            message.AddField(x =>
            {
                x.Name = "Healing";
                x.Value = $"{healingOverview}";
                x.IsInline = false;
            });
        }

        if (advancedLog)
        {
            message.AddField(x =>
            {
                x.Name = "Barrier";
                x.Value = $"{barrierOverview}";
                x.IsInline = false;
            });

            message.AddField(x =>
            {
                x.Name = "Times Downed";
                x.Value = $"{timesDownedOverview}";
                x.IsInline = false;
            });

            message.AddField(x =>
            {
                x.Name = "Distance From Tag";
                x.Value = $"{distanceOverview}";
                x.IsInline = false;
            });

            message.AddField(x =>
            {
                x.Name = "Aggregations";
                x.Value = $"{aggregations}";
                x.IsInline = false;
            });
        }

        message.Footer = new EmbedFooterBuilder()
        {
            Text = $"{await footerHandler.Generate(guildId)}",
            IconUrl = "https://i.imgur.com/tQ4LD6H.png"
        };

        // Timestamp
        message.Timestamp = DateTime.Now;

        // Building the message for use
        return message.Build();
    }
}