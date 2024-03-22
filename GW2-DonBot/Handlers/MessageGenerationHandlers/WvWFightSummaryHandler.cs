using System.Globalization;
using Discord;
using Discord.WebSocket;
using Extensions;
using Models;
using Models.Entities;
using Models.Statics;
using Services.PlayerServices;

namespace Handlers.MessageGenerationHandlers
{
    public class WvWFightSummaryHandler
    {
        private readonly IPlayerService _playerService;

        private readonly FooterHandler _footerHandler;

        public WvWFightSummaryHandler(IPlayerService playerService, FooterHandler footerHandler)
        {
            _playerService = playerService;
            _footerHandler = footerHandler;
        }

        public Embed Generate(EliteInsightDataModel data, bool advancedLog, Guild guild, DiscordSocketClient client)
        {
            var playerCount = 5;

            // Building the actual message to be sent
            var logLength = data.EncounterDuration?.TimeToSeconds() ?? 0;

            var friendlyCount = data.Players?.Count ?? 0;

            var enemyCount = data.Targets?.Count ?? 0;
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
            var friendlyOverview = "```";
            friendlyOverview      += $"{friendlyCountStr}  {friendlyDamageStr}  {friendlyDpsStr}  {friendlyDownsStr}  {friendlyDeathsStr}```";

            var enemyOverview = "```";
            enemyOverview      += $"{enemyCountStr}  {enemyDamageStr}  {enemyDpsStr}  {enemyDownsStr}  {enemyDeathsStr}```";

            // Damage overview
            var damageOverview = "```";

            var maxDamage = -1.0f;
            var maxDownContribution = -1.0f;
            var topDamage = gw2Players.OrderByDescending(s => s.Damage).Take(playerCount).ToList();
            var damageIndex = 1;
            foreach (var gw2Player in topDamage)
            {
                var name = gw2Player.CharacterName;
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

                damageOverview += $"{damageIndex.ToString().PadLeft(2, '0')}  {name?.ClipAt(ArcDpsDataIndices.NameClipLength) + EliteInsightExtensions.GetClassAppend(prof),-ArcDpsDataIndices.NameSizeLength}  {damageFloat.FormatNumber(maxDamage).PadCenter(7)}  {downContribution.FormatNumber(maxDownContribution).PadCenter(7)}\n";
                damageIndex++;
            }

            damageOverview += "```";

            // Cleanse overview
            var cleanseOverview = $"```";

            var topCleanses = gw2Players.OrderByDescending(s => s.Cleanses).Take(playerCount).ToList();
            var cleanseIndex = 1;
            foreach (var gw2Player in topCleanses)
            {
                var cleanses = gw2Player.Cleanses;
                var name = gw2Player.CharacterName;
                var prof = gw2Player.Profession;

                cleanseOverview += $"{cleanseIndex.ToString().PadLeft(2, '0')}  {name?.ClipAt(ArcDpsDataIndices.NameClipLength) + EliteInsightExtensions.GetClassAppend(prof),-ArcDpsDataIndices.NameSizeLength}  {cleanses.ToString(CultureInfo.InvariantCulture).PadCenter(16)}\n";
                cleanseIndex++;
            }

            cleanseOverview += "```";

            // Strip overview
            var stripOverview = "```";

            var topStrips = gw2Players.OrderByDescending(s => s.Strips).Take(playerCount).ToList();
            var stripIndex = 1;
            foreach (var gw2Player in topStrips)
            {
                var strips = gw2Player.Strips;
                var name = gw2Player.CharacterName;
                var prof = gw2Player.Profession;

                stripOverview += $"{stripIndex.ToString().PadLeft(2, '0')}  {name?.ClipAt(ArcDpsDataIndices.NameClipLength) + EliteInsightExtensions.GetClassAppend(prof),-ArcDpsDataIndices.NameSizeLength}  {strips.ToString(CultureInfo.InvariantCulture).PadCenter(16)}\n";
                stripIndex++;
            }

            stripOverview += "```";

            // Stab overview
            var stabOverview = "```";

            var topStabs = gw2Players.OrderByDescending(s => s.StabUpTime).Take(playerCount).ToList();
            var stabIndex = 1;
            foreach (var gw2Player in topStabs)
            {
                var stab = gw2Player.StabUpTime;
                var sub = gw2Player.SubGroup;
                var name = gw2Player.CharacterName;
                var prof = gw2Player.Profession;

                stabOverview += $"{stabIndex.ToString().PadLeft(2, '0')}  {name?.ClipAt(ArcDpsDataIndices.NameClipLength) + EliteInsightExtensions.GetClassAppend(prof),-ArcDpsDataIndices.NameSizeLength}  {sub.ToString().PadCenter(3)}  {(stab * 100).FormatPercentage().ToString(CultureInfo.InvariantCulture).PadCenter(11)}\n";
                stabIndex++;
            }

            stabOverview += "```";

            var healingOverview = "```";

            var topHealing = gw2Players.OrderByDescending(s => s.Healing).Take(playerCount).ToList();
            var healingIndex = 1;
            foreach (var gw2Player in topHealing)
            {
                var healing = gw2Player.Healing;
                var name = gw2Player.CharacterName;
                var prof = gw2Player.Profession;

                healingOverview += $"{healingIndex.ToString().PadLeft(2, '0')}  {name?.ClipAt(ArcDpsDataIndices.NameClipLength) + EliteInsightExtensions.GetClassAppend(prof),-ArcDpsDataIndices.NameSizeLength}  {healing.ToString(CultureInfo.InvariantCulture).PadCenter(16)}\n";
                healingIndex++;
            }

            healingOverview += "```";

            var distanceOverview = "```";
            var timesDownedOverview = "```";
            var interruptedEnemyOverview = "```";
            var barrierOverview = "```";
            var blindsAggregation = "```";
            var blocksAggregation = "```";
            var stripsAggregation = "```";
            var barrierAggregation = "```";

            if (advancedLog)
            {
                var topBarrier = gw2Players.OrderByDescending(s => s.BarrierGenerated).Take(playerCount).ToList();
                var barrierIndex = 1;
                foreach (var gw2Player in topBarrier)
                {
                    var barrier = gw2Player.BarrierGenerated;
                    var name = gw2Player.CharacterName;
                    var prof = gw2Player.Profession;

                    barrierOverview += $"{barrierIndex.ToString().PadLeft(2, '0')}  {name?.ClipAt(ArcDpsDataIndices.NameClipLength) + EliteInsightExtensions.GetClassAppend(prof),-ArcDpsDataIndices.NameSizeLength} {string.Empty,5} {barrier.ToString(CultureInfo.InvariantCulture)}\n";
                    barrierIndex++;
                }
                barrierOverview += "```";

                var topDistance = gw2Players.OrderByDescending(s => s.DistanceFromTag).Take(playerCount).ToList();
                var distanceIndex = 1;
                foreach (var gw2Player in topDistance)
                {
                    var distance = gw2Player.DistanceFromTag;
                    var name = gw2Player.CharacterName;
                    var prof = gw2Player.Profession;

                    distanceOverview += $"{distanceIndex.ToString().PadLeft(2, '0')}  {name?.ClipAt(ArcDpsDataIndices.NameClipLength) + EliteInsightExtensions.GetClassAppend(prof),-ArcDpsDataIndices.NameSizeLength} {string.Empty,1} {distance.ToString(CultureInfo.InvariantCulture)}\n";
                    distanceIndex++;
                }
                distanceOverview += "```";

                var topTimesDowned = gw2Players.OrderByDescending(s => s.TimesDowned).Take(playerCount).ToList();
                var timesDownedIndex = 1;
                foreach (var gw2Player in topTimesDowned)
                {
                    var timesDowned = gw2Player.TimesDowned;
                    var name = gw2Player.CharacterName;
                    var prof = gw2Player.Profession;

                    timesDownedOverview += $"{timesDownedIndex.ToString().PadLeft(2, '0')}  {name?.ClipAt(ArcDpsDataIndices.NameClipLength) + EliteInsightExtensions.GetClassAppend(prof),-ArcDpsDataIndices.NameSizeLength} {string.Empty,3} {timesDowned.ToString(CultureInfo.InvariantCulture)}\n";
                    timesDownedIndex++;
                }
                
                timesDownedOverview += "```";

                blindsAggregation += $"{string.Empty.PadLeft(21) + gw2Players.Sum(s => s.NumberOfHitsWhileBlinded).ToString().PadRight(4) + string.Empty.PadLeft(10) + gw2Players.Sum(s => s.NumberOfMissesAgainst).ToString(CultureInfo.CurrentCulture)}";
                blindsAggregation += "```";

                blocksAggregation += $"{string.Empty.PadLeft(21) + gw2Players.Sum(s => s.NumberOfTimesBlockedAttack).ToString(CultureInfo.CurrentCulture).PadRight(4) + string.Empty.PadLeft(10) + gw2Players.Sum(s => s.NumberOfTimesEnemyBlockedAttack)}";
                blocksAggregation += "```";

                stripsAggregation += $"{string.Empty.PadLeft(21) + gw2Players.Sum(s => s.Strips).ToString(CultureInfo.CurrentCulture).PadRight(4) + string.Empty.PadLeft(10) + gw2Players.Sum(s => s.NumberOfBoonsRipped).ToString(CultureInfo.CurrentCulture)}";
                stripsAggregation += "```";

                var totalDmg = Convert.ToSingle(gw2Players.Sum(s => s.DamageTaken));
                var totalBarrierMitigation = Convert.ToSingle(gw2Players.Sum(s => s.BarrierMitigation));

                barrierAggregation += $"{string.Empty.PadLeft(1) + totalDmg.FormatNumber(totalDmg).ToString(CultureInfo.CurrentCulture).PadRight(6) + string.Empty.PadLeft(9) + totalBarrierMitigation.FormatNumber(totalBarrierMitigation).ToString(CultureInfo.CurrentCulture).PadRight(6) + string.Empty.PadLeft(10) + (totalDmg - totalBarrierMitigation).FormatNumber(totalDmg - totalBarrierMitigation).ToString(CultureInfo.CurrentCulture)} ({Math.Round((totalBarrierMitigation / totalDmg) * 100, 2)}%)";
                barrierAggregation += "```";
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

            if (!advancedLog)
            {
                message.AddField(x =>
                {
                    x.Name = "```  #            Name             Damage      Down C  ```";
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

                message.AddField(x =>
                {
                    x.Name = "```  #            Name              Sub      Stab      ```";
                    x.Value = $"{stabOverview}";
                    x.IsInline = false;
                });

                message.AddField(x =>
                {
                    x.Name = "```  #            Name                   Healing       ```";
                    x.Value = $"{healingOverview}";
                    x.IsInline = false;
                });
            }
            
            if (advancedLog)
            {
                message.AddField(x =>
                {
                    x.Name = "```  #            Name                   Barrier Gen   ```";
                    x.Value = $"{barrierOverview}";
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
                    x.Name = "```  #            Name                Times Downed     ```";
                    x.Value = $"{timesDownedOverview}";
                    x.IsInline = false;
                });

                message.AddField(x =>
                {
                    x.Name = "```  #            Name              Distance From Tag  ```";
                    x.Value = $"{distanceOverview}";
                    x.IsInline = false;
                });

                message.AddField(x =>
                {
                    x.Name = "```  Attacks Missed         Ours            Theirs     ```";
                    x.Value = $"{blindsAggregation}";      
                    x.IsInline = false;
                });

                message.AddField(x =>
                {
                    x.Name = "```  Attacks Blocked        Ours            Theirs     ```";
                    x.Value = $"{blocksAggregation}";
                    x.IsInline = false;
                });

                message.AddField(x =>
                {
                    x.Name = "```  Boons Stripped         Ours            Theirs     ```";
                    x.Value = $"{stripsAggregation}";
                    x.IsInline = false;
                });

                message.AddField(x =>
                {
                    x.Name = "```  Damage Taken     Barrier Mit        Diff          ```";
                    x.Value = $"{barrierAggregation}";
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
