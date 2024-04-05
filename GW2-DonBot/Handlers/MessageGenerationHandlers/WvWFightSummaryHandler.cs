using System;
using System.Globalization;
using Discord;
using Discord.WebSocket;
using Extensions;
using Models;
using Models.Entities;
using Models.Enums;
using Models.Statics;
using Services.PlayerServices;

namespace Handlers.MessageGenerationHandlers
{
    public class WvWFightSummaryHandler
    {
        private readonly IPlayerService _playerService;

        private readonly DatabaseContext _databaseContext;

        private readonly FooterHandler _footerHandler;

        public WvWFightSummaryHandler(IPlayerService playerService, FooterHandler footerHandler, DatabaseContext databaseContext)
        {
            _playerService = playerService;
            _footerHandler = footerHandler;
            _databaseContext = databaseContext;
        }

        public Embed Generate(EliteInsightDataModel data, bool advancedLog, Guild guild, DiscordSocketClient client)
        {
            var playerCount = 5;

            // Building the actual message to be sent
            var logLength = data.EncounterDuration?.TimeToSeconds() ?? 0;

            var friendlyCount = data.Players?.Count ?? 0;

            // remove one from target dummy
            var enemyCount = (data.Targets?.Count - 1) ?? 0;
            var enemyDamage = data.Targets?
                .Sum(player => player.Details?.DmgDistributions?.Any() ?? false
                    ? player.Details?.DmgDistributions[0].ContributedDamage
                    : 0) ?? 0;

            var enemyDps = enemyDamage / logLength;

            var fightPhase = data.Phases?.Any() ?? false
                ? data.Phases[0]
                : new ArcDpsPhase();

            var healingPhase = data.HealingStatsExtension?.HealingPhases?.FirstOrDefault() ?? new HealingPhase();
            var barrierPhase = data.BarrierStatsExtension?.BarrierPhases?.FirstOrDefault() ?? new BarrierPhase();

            var friendlyDowns = fightPhase.DefStats?
                .Sum(playerDefStats => playerDefStats.Count >= ArcDpsDataIndices.FriendlyDownIndex
                    ? playerDefStats[ArcDpsDataIndices.FriendlyDownIndex].Double
                    : 0) ?? 0;

            var friendlyDeaths = fightPhase.DefStats?
                .Sum(playerDefStats => playerDefStats.Count >= ArcDpsDataIndices.FriendlyDeathIndex
                    ? playerDefStats[ArcDpsDataIndices.FriendlyDeathIndex].Double
                    : 0) ?? 0;

            var enemyDowns = fightPhase.OffensiveStatsTargets?
                .Sum(playerOffStats => playerOffStats.Sum(playerOffTargetStats => playerOffTargetStats?.Count >= ArcDpsDataIndices.EnemyDownIndex
                    ? playerOffTargetStats?[ArcDpsDataIndices.EnemyDownIndex]
                    : 0)) ?? 0;

            var enemyDeaths = fightPhase.OffensiveStatsTargets?
                .Sum(playerOffStats => playerOffStats.Sum(playerOffTargetStats => playerOffTargetStats?.Count >= ArcDpsDataIndices.EnemyDeathIndex
                    ? playerOffTargetStats?[ArcDpsDataIndices.EnemyDeathIndex]
                    : 0)) ?? 0;

            var gw2Players = _playerService.GetGw2Players(data, fightPhase, healingPhase, barrierPhase);

            var friendlyDamage = gw2Players.Sum(s => s.Damage);
            var friendlyDps = friendlyDamage / logLength;

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

            if (!advancedLog && guild.StreamLogChannelId.HasValue)
            {
                var streamMessage =
$@"```
Who     Count    Damage      DPS       Downs     Deaths  
Friends {friendlyCountStr.Trim(),-3}      {friendlyDamageStr.Trim(),-7}     {friendlyDpsStr.Trim(),-6}    {friendlyDownsStr.Trim(),-3}       {friendlyDeathsStr.Trim(),-3}   
Enemies {enemyCountStr.Trim(),-3}      {enemyDamageStr.Trim(),-7}     {enemyDpsStr.Trim(),-6}    {enemyDownsStr.Trim(),-3}       {enemyDeathsStr.Trim(),-3}
```";

                if (client.GetChannel((ulong)guild.StreamLogChannelId) is ITextChannel streamLogChannel)
                {
                    streamLogChannel.SendMessageAsync(text: streamMessage);
                }
            }

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
            var friendlyOverview = "```Friends   DMG      DPS     Downs     Deaths         \n";
            friendlyOverview      += $"{friendlyCountStr.Trim(),-3}{string.Empty,7}{friendlyDamageStr.Trim(),-7}{string.Empty,2}{friendlyDpsStr.Trim(),-6}{string.Empty,2}{friendlyDownsStr.Trim(),-3}{string.Empty,7}{friendlyDeathsStr.Trim(),-3}```";

            var enemyOverview = "```Enemies   DMG      DPS     Downs     Deaths         \n";
            enemyOverview      += $"{enemyCountStr.Trim(),-3}{string.Empty,7}{enemyDamageStr.Trim(),-7}{string.Empty,2}{enemyDpsStr.Trim(),-6}{string.Empty,2}{enemyDownsStr.Trim(),-3}{string.Empty,7}{enemyDeathsStr.Trim(),-3}```";

            var dateStartString = data.EncounterStart;
            var dateTimeStart = DateTime.ParseExact(dateStartString, "yyyy-MM-dd HH:mm:ss zzz", CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal);

            var dateEndString = data.EncounterEnd;
            var dateTimeEnd = DateTime.ParseExact(dateEndString, "yyyy-MM-dd HH:mm:ss zzz", CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal);

            var duration = dateTimeEnd - dateTimeStart;

            if (!advancedLog)
            {
                var fightLog = new FightLog
                {
                    GuildId = guild.GuildId,
                    Url = data.Url ?? string.Empty,
                    FightType = (short)FightTypesEnum.wvw,
                    FightStart = dateTimeStart,
                    FightDurationInMs = (long)duration.TotalMilliseconds,
                    IsSuccess = data.Success
                };

                _databaseContext.Add(fightLog);
                _databaseContext.SaveChanges();

                var playerFights = gw2Players.Select(gw2Player => new PlayerFightLog
                {
                    FightLogId = fightLog.FightLogId,
                    GuildWarsAccountName = gw2Player.AccountName,
                    Damage = gw2Player.Damage,
                    QuicknessDuration = Math.Round(Convert.ToDecimal(gw2Player.TotalQuick), 2),
                    AlacDuration = Math.Round(Convert.ToDecimal(gw2Player.TotalAlac), 2),
                    SubGroup = gw2Player.SubGroup,
                    DamageDownContribution = gw2Player.DamageDownContribution,
                    Cleanses = Convert.ToInt64(gw2Player.Cleanses),
                    Strips = Convert.ToInt64(gw2Player.Strips),
                    StabGenerated = Math.Round(Convert.ToDecimal(gw2Player.StabUpTime), 2),
                    Healing = gw2Player.Healing,
                    BarrierGenerated = gw2Player.BarrierGenerated,
                    DistanceFromTag = Math.Round(Convert.ToDecimal(gw2Player.DistanceFromTag), 2),
                    TimesDowned = Convert.ToInt32(gw2Player.TimesDowned),
                    Interrupts = gw2Player.Interrupts,
                    NumberOfHitsWhileBlinded = gw2Player.NumberOfHitsWhileBlinded,
                    NumberOfMissesAgainst = Convert.ToInt64(gw2Player.NumberOfMissesAgainst),
                    NumberOfTimesBlockedAttack = Convert.ToInt64(gw2Player.NumberOfTimesBlockedAttack),
                    NumberOfTimesEnemyBlockedAttack = gw2Player.NumberOfTimesEnemyBlockedAttack,
                    NumberOfBoonsRipped = Convert.ToInt64(gw2Player.NumberOfBoonsRipped),
                    DamageTaken = Convert.ToInt64(gw2Player.DamageTaken),
                    BarrierMitigation = Convert.ToInt64(gw2Player.BarrierMitigation)
                })
                .ToList();

                _databaseContext.AddRange(playerFights);
                _databaseContext.SaveChanges();
            }

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
                x.Name = "``` ```";
                x.Value = $"{friendlyOverview.ReplaceSpacesWithNonBreaking()}";
                x.IsInline = false;
            });

            message.AddField(x =>
            {
                x.Name = "``` ```";
                x.Value = $"{enemyOverview.ReplaceSpacesWithNonBreaking()}";
                x.IsInline = false;
            });

            return GenerateMessage(advancedLog, playerCount, gw2Players, message);
        }

        public Embed GenerateMessage(bool advancedLog, int playerCount, List<Gw2Player> gw2Players, EmbedBuilder message)
        {
            // Damage overview
            var damageOverview = "```#    Name                   Damage    Down C                     \n";

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

                damageOverview += $"{damageIndex.ToString().PadLeft(2, '0')}{string.Empty,3}{name?.ClipAt(ArcDpsDataIndices.NameClipLength) + EliteInsightExtensions.GetClassAppend(prof),-ArcDpsDataIndices.NameSizeLength}{string.Empty,2}{damageFloat.FormatNumber(maxDamage),-8}{string.Empty,2}{downContribution.FormatNumber(maxDownContribution),-7}\n";
                damageIndex++;
            }

            damageOverview += "```";

            // Cleanse overview
            var cleanseOverview = $"```#    Name                   Cleanses                     \n";

            var topCleanses = gw2Players.OrderByDescending(s => s.Cleanses).Take(playerCount).ToList();
            var cleanseIndex = 1;
            foreach (var gw2Player in topCleanses)
            {
                var cleanses = gw2Player.Cleanses;
                var name = !string.IsNullOrEmpty(gw2Player.CharacterName) ? gw2Player.CharacterName : gw2Player.AccountName;
                var prof = gw2Player.Profession;

                cleanseOverview += $"{cleanseIndex.ToString().PadLeft(2, '0')}{string.Empty,3}{name?.ClipAt(ArcDpsDataIndices.NameClipLength) + EliteInsightExtensions.GetClassAppend(prof),-ArcDpsDataIndices.NameSizeLength}{string.Empty,2}{cleanses.ToString(CultureInfo.InvariantCulture),-5}\n";
                cleanseIndex++;
            }

            cleanseOverview += "```";

            // Strip overview
            var stripOverview = "```#    Name                   Strips                     \n";

            var topStrips = gw2Players.OrderByDescending(s => s.Strips).Take(playerCount).ToList();
            var stripIndex = 1;
            foreach (var gw2Player in topStrips)
            {
                var strips = gw2Player.Strips;
                var name = !string.IsNullOrEmpty(gw2Player.CharacterName) ? gw2Player.CharacterName : gw2Player.AccountName;
                var prof = gw2Player.Profession;

                stripOverview += $"{stripIndex.ToString().PadLeft(2, '0')}{string.Empty,3}{name?.ClipAt(ArcDpsDataIndices.NameClipLength) + EliteInsightExtensions.GetClassAppend(prof),-ArcDpsDataIndices.NameSizeLength}{string.Empty,2}{strips.ToString(CultureInfo.InvariantCulture),-5}\n";
                stripIndex++;
            }

            stripOverview += "```";

            // Stab overview
            var stabOverview = "```#    Name                   Sub  Stab                     \n\n";

            var topStabs = gw2Players.OrderByDescending(s => s.StabUpTime).Take(playerCount).ToList();
            var stabIndex = 1;
            foreach (var gw2Player in topStabs)
            {
                var stab = gw2Player.StabUpTime;
                var sub = gw2Player.SubGroup;
                var name = !string.IsNullOrEmpty(gw2Player.CharacterName) ? gw2Player.CharacterName : gw2Player.AccountName;
                var prof = gw2Player.Profession;

                stabOverview += $"{stabIndex.ToString().PadLeft(2, '0')}{string.Empty,3}{name?.ClipAt(ArcDpsDataIndices.NameClipLength) + EliteInsightExtensions.GetClassAppend(prof),-ArcDpsDataIndices.NameSizeLength}{string.Empty,2}{sub.ToString(),-3}{string.Empty,2}{(stab * 100).FormatPercentage().ToString(CultureInfo.InvariantCulture),-11}\n";
                stabIndex++;
            }

            stabOverview += "```";

            var healingOverview = "```#    Name                   Healing                     \n\n";

            var topHealing = gw2Players.OrderByDescending(s => s.Healing).Take(playerCount).ToList();
            var healingIndex = 1;
            foreach (var gw2Player in topHealing)
            {
                var healing = gw2Player.Healing;
                var name = !string.IsNullOrEmpty(gw2Player.CharacterName) ? gw2Player.CharacterName : gw2Player.AccountName;
                var prof = gw2Player.Profession;

                healingOverview += $"{healingIndex.ToString().PadLeft(2, '0')}{string.Empty,3}{name?.ClipAt(ArcDpsDataIndices.NameClipLength) + EliteInsightExtensions.GetClassAppend(prof),-ArcDpsDataIndices.NameSizeLength}{string.Empty,2}{healing.ToString(CultureInfo.InvariantCulture),-16}\n";
                healingIndex++;
            }

            healingOverview += "```";

            var distanceOverview = "```#    Name                    Distance From Tag                     \n";
            var timesDownedOverview = "```#    Name                   Times Downed                     \n";
            var interruptedEnemyOverview = "```";
            var barrierOverview = "```#    Name                   Barrier Gen                     \n";
            var blindsAggregation = "```Attacks Missed         Ours          Theirs \n";
            var blocksAggregation = "```Attacks Blocked        Ours          Theirs \n";
            var stripsAggregation = "```Boons Stripped         Ours          Theirs \n";
            var barrierAggregation = "```Damage Taken    Barrier Mit   Diff \n";

            if (advancedLog)
            {
                var topBarrier = gw2Players.OrderByDescending(s => s.BarrierGenerated).Take(playerCount).ToList();
                var barrierIndex = 1;
                foreach (var gw2Player in topBarrier)
                {
                    var barrier = gw2Player.BarrierGenerated;
                    var name = !string.IsNullOrEmpty(gw2Player.CharacterName) ? gw2Player.CharacterName : gw2Player.AccountName;
                    var prof = gw2Player.Profession;

                    barrierOverview += $"{barrierIndex.ToString().PadLeft(2, '0')}{string.Empty,3}{name?.ClipAt(ArcDpsDataIndices.NameClipLength) + EliteInsightExtensions.GetClassAppend(prof),-ArcDpsDataIndices.NameSizeLength}{string.Empty,2}{barrier.ToString(CultureInfo.InvariantCulture)}\n";
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

                    distanceOverview += $"{distanceIndex.ToString().PadLeft(2, '0')}{string.Empty,3}{name?.ClipAt(ArcDpsDataIndices.NameClipLength) + EliteInsightExtensions.GetClassAppend(prof),-ArcDpsDataIndices.NameSizeLength}{string.Empty,3}{distance.ToString(CultureInfo.InvariantCulture)}\n";
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

                    timesDownedOverview += $"{timesDownedIndex.ToString().PadLeft(2, '0')}{string.Empty,3}{name?.ClipAt(ArcDpsDataIndices.NameClipLength) + EliteInsightExtensions.GetClassAppend(prof),-ArcDpsDataIndices.NameSizeLength}{string.Empty,2}{timesDowned.ToString(CultureInfo.InvariantCulture)}\n";
                    timesDownedIndex++;
                }

                timesDownedOverview += "```";

                blindsAggregation += $"{string.Empty,23}{gw2Players.Sum(s => s.NumberOfHitsWhileBlinded).ToString(),-4}{string.Empty,10}{gw2Players.Sum(s => s.NumberOfMissesAgainst).ToString(CultureInfo.CurrentCulture)}";
                blindsAggregation += "```";

                blocksAggregation += $"{string.Empty,23}{gw2Players.Sum(s => s.NumberOfTimesBlockedAttack).ToString(CultureInfo.CurrentCulture),-4}{string.Empty.PadLeft(10) + gw2Players.Sum(s => s.NumberOfTimesEnemyBlockedAttack)}";
                blocksAggregation += "```";

                stripsAggregation += $"{string.Empty,23}{gw2Players.Sum(s => s.Strips).ToString(CultureInfo.CurrentCulture),-4}{string.Empty,10}{gw2Players.Sum(s => s.NumberOfBoonsRipped).ToString(CultureInfo.CurrentCulture)}";
                stripsAggregation += "```";

                var totalDmg = Convert.ToSingle(gw2Players.Sum(s => s.DamageTaken));
                var totalBarrierMitigation = Convert.ToSingle(gw2Players.Sum(s => s.BarrierMitigation));

                barrierAggregation += $"{totalDmg.FormatNumber(totalDmg).ToString(CultureInfo.CurrentCulture),-6}{string.Empty,10}{totalBarrierMitigation.FormatNumber(totalBarrierMitigation).ToString(CultureInfo.CurrentCulture),-6}{string.Empty,8}{(totalDmg - totalBarrierMitigation).FormatNumber(totalDmg - totalBarrierMitigation).ToString(CultureInfo.CurrentCulture)}({Math.Round((totalBarrierMitigation / totalDmg) * 100, 2)}%)";
                barrierAggregation += "```";
            }

            if (!advancedLog)
            {
                message.AddField(x =>
                {
                    x.Name = "``` ```";
                    x.Value = $"{damageOverview.ReplaceSpacesWithNonBreaking()}";
                    x.IsInline = false;
                });

                message.AddField(x =>
                {
                    x.Name = "``` ```";
                    x.Value = $"{cleanseOverview.ReplaceSpacesWithNonBreaking()}";
                    x.IsInline = false;
                });

                message.AddField(x =>
                {
                    x.Name = "```  ```";
                    x.Value = $"{stripOverview.ReplaceSpacesWithNonBreaking()}";
                    x.IsInline = false;
                });

                message.AddField(x =>
                {
                    x.Name = "``` ```";
                    x.Value = $"{stabOverview.ReplaceLineEndings()}";
                    x.IsInline = false;
                });

                message.AddField(x =>
                {
                    x.Name = "``` ```";
                    x.Value = $"{healingOverview.ReplaceLineEndings()}";
                    x.IsInline = false;
                });
            }

            if (advancedLog)
            {
                message.AddField(x =>
                {
                    x.Name = "``` ```";
                    x.Value = $"{barrierOverview.ReplaceSpacesWithNonBreaking()}";
                    x.IsInline = false;
                });

                // TODO: update all this to be config driven per guild
                if (false)
                {
                    message.AddField(x =>
                    {
                        x.Name = "```  #            Name                  Interrupts     ```";
                        x.Value = $"{interruptedEnemyOverview}";
                        x.IsInline = false;
                    });
                }

                message.AddField(x =>
                {
                    x.Name = "``` ```";
                    x.Value = $"{timesDownedOverview.ReplaceSpacesWithNonBreaking()}";
                    x.IsInline = false;
                });

                message.AddField(x =>
                {
                    x.Name = "``` ```";
                    x.Value = $"{distanceOverview.ReplaceSpacesWithNonBreaking()}";
                    x.IsInline = false;
                });

                message.AddField(x =>
                {
                    x.Name = "``` ```";
                    x.Value = $"{blindsAggregation.ReplaceSpacesWithNonBreaking()}";
                    x.IsInline = false;
                });

                message.AddField(x =>
                {
                    x.Name = "``` ```";
                    x.Value = $"{blocksAggregation.ReplaceSpacesWithNonBreaking()}";
                    x.IsInline = false;
                });

                message.AddField(x =>
                {
                    x.Name = "``` ```";
                    x.Value = $"{stripsAggregation.ReplaceSpacesWithNonBreaking()}";
                    x.IsInline = false;
                });

                message.AddField(x =>
                {
                    x.Name = "``` ```";
                    x.Value = $"{barrierAggregation.ReplaceSpacesWithNonBreaking()}";
                    x.IsInline = false;
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
            return message.Build();
        }
    }
}
