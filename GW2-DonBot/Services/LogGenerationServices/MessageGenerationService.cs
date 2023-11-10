using Discord;
using Discord.Webhook;
using Discord.WebSocket;
using Extensions;
using GW2DonBot.Models;
using Microsoft.EntityFrameworkCore;
using Models;
using Models.Entities;
using Models.GW2Api;
using System.Globalization;

namespace Services.LogGenerationServices
{
    public class MessageGenerationService: IMessageGenerationService
    {
        private readonly DatabaseContext _databaseContext;

        public MessageGenerationService(IDatabaseContext databaseContext)
        {
            _databaseContext = databaseContext.GetDatabaseContext();
        }

        private const int FriendlyDownIndex = 12;
        private const int FriendlyDeathIndex = 14;
        private const int EnemyDeathIndex = 12;
        private const int EnemyDownIndex = 13;
        private const int PlayerCleansesIndex = 2;
        private const int PlayerStripsIndex = 4;
        private const int BoonStabDimension1Index = 8;
        private const int BoonStabDimension2Index = 1;
        private const int DistanceFromTagIndex = 6;
        private const int InterruptsIndex = 7;
        private const int HealingDimension1Index = 0;
        private const int HealingDimension2Index = 0;
        private const int NameClipLength = 15;
        private const int NameSizeLength = 21;
        private const int PlayersListed = 5;
        private const int AdvancedPlayersListed = 10;
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

        public async Task<Embed> GenerateWvWFightSummary(EliteInsightDataModel data, bool advancedLog, bool setPlayerPoint, Guild guild)
        {
            if (setPlayerPoint)
            {
                await SetPlayerPoints(data);
            }

            var playerCount = advancedLog ? AdvancedPlayersListed : PlayersListed;

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

            var gw2Players = GetGw2Players(data, fightPhase, healingPhase, data.BarrierStatsExtension.BarrierPhases.FirstOrDefault());

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

            if (!advancedLog)
            {
                var streamMessage =
$@"```
Who     Count    Damage      DPS       Downs     Deaths  
Friends {friendlyCountStr.Trim(),-3}      {friendlyDamageStr.Trim(),-7}     {friendlyDpsStr.Trim(),-6}    {friendlyDownsStr.Trim(),-3}       {friendlyDeathsStr.Trim(),-3}   
Enemies {enemyCountStr.Trim(),-3}      {enemyDamageStr.Trim(),-7}     {enemyDpsStr.Trim(),-6}    {enemyDownsStr.Trim(),-3}       {enemyDeathsStr.Trim(),-3}
```";

                var playerReportWebhook = new DiscordWebhookClient(guild.StreamLogsWebhook);
                playerReportWebhook.SendMessageAsync(text: streamMessage, username: "GW2-DonBot", avatarUrl: "https://i.imgur.com/tQ4LD6H.png");
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

                damageOverview += $"{damageIndex.ToString().PadLeft(2, '0')}  {(name?.ClipAt(NameClipLength) + EliteInsightExtensions.GetClassAppend(prof)).PadRight(NameSizeLength)}  {damageFloat.FormatNumber(maxDamage).PadCenter(7)}  {(damageFloat / logLength).FormatNumber(maxDamage / logLength).PadCenter(7)}\n";
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

                cleanseOverview += $"{cleanseIndex.ToString().PadLeft(2, '0')}  {(name?.ClipAt(NameClipLength) + EliteInsightExtensions.GetClassAppend(prof)).PadRight(NameSizeLength)}  {cleanses.ToString(CultureInfo.InvariantCulture).PadCenter(16)}\n";
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

                stripOverview += $"{stripIndex.ToString().PadLeft(2, '0')}  {(name?.ClipAt(NameClipLength) + EliteInsightExtensions.GetClassAppend(prof)).PadRight(NameSizeLength)}  {strips.ToString(CultureInfo.InvariantCulture).PadCenter(16)}\n";
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

                stabOverview += $"{stabIndex.ToString().PadLeft(2, '0')}  {(name?.ClipAt(NameClipLength) + EliteInsightExtensions.GetClassAppend(prof)).PadRight(NameSizeLength)}  {sub.ToString().PadCenter(3)}  {(stab * 100).FormatPercentage().ToString(CultureInfo.InvariantCulture).PadCenter(11)}\n";
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

                healingOverview += $"{healingIndex.ToString().PadLeft(2, '0')}  {(name?.ClipAt(NameClipLength) + EliteInsightExtensions.GetClassAppend(prof)).PadRight(NameSizeLength)}  {healing.ToString(CultureInfo.InvariantCulture).PadCenter(16)}\n";
                healingIndex++;
            }

            healingOverview += "```";

            var distanceOverview = "```";
            var timesDownedOverview = "```";
            var interruptedEnemyOverview = "```";
            var barrierOverview = "```";

            if (advancedLog)
            {
                var topBarrier = gw2Players.OrderByDescending(s => s.Barrier).Take(playerCount).ToList();
                var barrierIndex = 1;
                foreach (var gw2Player in topBarrier)
                {
                    var barrier = gw2Player.Barrier;
                    var name = gw2Player.CharacterName;
                    var prof = gw2Player.Profession;

                    barrierOverview += $"{barrierIndex.ToString().PadLeft(2, '0')}  {(name?.ClipAt(NameClipLength) + EliteInsightExtensions.GetClassAppend(prof)).PadRight(NameSizeLength)}  {barrier.ToString(CultureInfo.InvariantCulture).PadCenter(16)}\n";
                    barrierIndex++;
                }
                barrierOverview += "```";

                var topDistance = gw2Players.OrderBy(s => s.DistanceFromTag).Take(playerCount * 2).ToList();
                var distanceIndex = 1;
                foreach (var gw2Player in topDistance)
                {
                    var distance = gw2Player.DistanceFromTag;
                    var name = gw2Player.CharacterName;
                    var prof = gw2Player.Profession;

                    distanceOverview += $"{distanceIndex.ToString().PadLeft(2, '0')}  {(name?.ClipAt(NameClipLength) + EliteInsightExtensions.GetClassAppend(prof)).PadRight(NameSizeLength)}  {distance.ToString(CultureInfo.InvariantCulture).PadCenter(16)}\n";
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

                    timesDownedOverview += $"{timesDownedIndex.ToString().PadLeft(2, '0')}  {(name?.ClipAt(NameClipLength) + EliteInsightExtensions.GetClassAppend(prof)).PadRight(NameSizeLength)}  {timesDowned.ToString(CultureInfo.InvariantCulture).PadCenter(16)}\n";
                    timesDownedIndex++;
                }

                timesDownedOverview += "```";

                var topInterruptedEnemy = gw2Players.OrderByDescending(s => s.Interrupts).Take(playerCount).ToList();
                var interruptedEnemyIndex = 1;
                foreach (var gw2Player in topInterruptedEnemy)
                {
                    var interrupts = gw2Player.Interrupts;
                    var name = gw2Player.CharacterName;
                    var prof = gw2Player.Profession;

                    interruptedEnemyOverview += $"{interruptedEnemyIndex.ToString().PadLeft(2, '0')}  {(name?.ClipAt(NameClipLength) + EliteInsightExtensions.GetClassAppend(prof)).PadRight(NameSizeLength)}  {interrupts.ToString(CultureInfo.InvariantCulture).PadCenter(16)}\n";
                    interruptedEnemyIndex++;
                }

                interruptedEnemyOverview += "```";
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

            if (advancedLog)
            {
                message.AddField(x =>
                {
                    x.Name = "```  #            Name                   Barrier       ```";
                    x.Value = $"{barrierOverview}";
                    x.IsInline = false;
                });
                message.AddField(x =>
                {
                    x.Name = "```  #            Name                  Interrupts     ```";
                    x.Value = $"{interruptedEnemyOverview}";
                    x.IsInline = false;
                });

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
            }

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

        public async void GenerateWvWPlayerReport(Guild guild, SocketGuild discordGuild, string guildConfigurationWvwPlayerActivityReportWebhook)
        {
            var accounts = await _databaseContext.Account.Where(acc => acc.Gw2ApiKey != null).ToListAsync();
            var position = 1;
            var accountsPerMessage = 15;

            var message = new EmbedBuilder
            {
                Title = "WvW Player Report\n",
                Description = "**WvW player report:**\n",
                Color = (Color)System.Drawing.Color.FromArgb(230, 231, 232),
                Author = new EmbedAuthorBuilder()
                {
                    Name = "GW2-DonBot",
                    Url = "https://github.com/LoganWal/GW2-DonBot",
                    IconUrl = "https://i.imgur.com/tQ4LD6H.png"
                },
                Footer = new EmbedFooterBuilder()
                {
                    Text = $"{GetJokeFooter()}",
                    IconUrl = "https://i.imgur.com/tQ4LD6H.png"
                },
                Timestamp = DateTime.Now
            };

            if (guild.WvwPlayerActivityReportWebhook != null)
            {
                for (var i = 0; i < accounts.Count / accountsPerMessage + 1; i++)
                {
                    var accountOverview = "```";
                    message = new EmbedBuilder();
                    var useLimit = false;
                    var limit = 0;

                    if (position + accountsPerMessage > accounts.Count)
                    {
                        limit = accounts.Count % accountsPerMessage;
                    }

                    var playerBatch = accounts.OrderByDescending(o => o.LastWvwLogDateTime).Take(new Range(accountsPerMessage * i, !useLimit ? accountsPerMessage * i + accountsPerMessage : limit)).ToList();

                    if (!playerBatch.Any())
                    {
                        break;
                    }

                    foreach (var account in playerBatch)
                    {
                        var name = account.Gw2AccountName;
                        var server = "Unknown";
                        if (account.World.HasValue)
                        {
                            server = ((GW2WorldEnum)account.World).ToString();
                        }

                        var lastLogDateTime = account.LastWvwLogDateTime.HasValue ? account.LastWvwLogDateTime.Value.AddHours(10).ToString("yyyy-MM-dd") : "Never";

                        accountOverview += $"{name.ClipAt(34).PadRight(34)} {server.ClipAt(7).PadRight(7)}   {lastLogDateTime.ClipAt(12).PadRight(12)} \n";
                        position++;
                    }

                    accountOverview += "```";

                    message.AddField(x =>
                    {
                        x.Name = "``` Name                                    Server     Last Log (AEST)   ```\n";
                        x.Value = $"{accountOverview}";
                        x.IsInline = false;
                    });

                    var playerMessage = message.Build();

                    var playerReport = new DiscordWebhookClient(guildConfigurationWvwPlayerActivityReportWebhook);
                    await playerReport.SendMessageAsync(text: "", username: "GW2-DonBot", avatarUrl: "https://i.imgur.com/tQ4LD6H.png", embeds: new[] { playerMessage });
                }
            }
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

            progressTitle = $"Overall [{progress.FormatPercentage()}]".PadCenter(EmbedTitleCharacterLength);

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

                phaseProgressTitle = $"{phaseName} [{phaseProgress.FormatPercentage()}]".PadCenter(EmbedTitleCharacterLength);

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

        public async Task<Embed> GenerateWvWPlayerSummary(SocketGuild discordGuild, Guild gw2Guild)
        {
            var accounts = await _databaseContext.Account.Where(acc => acc.Gw2ApiKey != null).ToListAsync();
            var position = 1;

            var message = new EmbedBuilder
            {
                Title = "Report - WvW points\n",
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
                for (var i = 0; i < accounts.Count / 20 + 1; i++)
                {
                    var accountOverview = "```";
                    var useLimit = false;
                    var limit = 0;

                    if (position + 20 > accounts.Count)
                    {
                        limit = accounts.Count % 20;
                    }

                    var playerBatch = accounts.OrderByDescending(o => o.Points).Take(new Range(20 * i, !useLimit ? 20 * i + 20 : limit)).ToList();

                    if (!playerBatch.Any())
                    {
                        break;
                    }

                    foreach (var account in playerBatch)
                    {
                        var points = account.Points;
                        var name = account.Gw2AccountName;
                        var pointsDiff = Math.Round(account.Points - account.PreviousPoints);

                        accountOverview += $"{position.ToString().PadLeft(3, '0')}  {name.ClipAt(23).PadRight(23)}  {Convert.ToInt32(points)}(+{Convert.ToInt32(pointsDiff)})\n";
                        position++;
                    }

                    accountOverview += "```";

                    message.AddField(x =>
                    {
                        x.Name = "``` #     Name                        Points         ```\n";
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

        private async Task SetPlayerPoints(EliteInsightDataModel eliteInsightDataModel)
        {
            if (eliteInsightDataModel.Players == null)
            {
                return;
            }

            var accounts = await _databaseContext.Account.Where(acc => acc.Gw2ApiKey != null).ToListAsync();
            if (!accounts.Any())
            {
                return;
            }

            var fightPhase = eliteInsightDataModel.Phases?.Any() ?? false
                ? eliteInsightDataModel.Phases[0]
                : new ArcDpsPhase();

            var healingPhase = eliteInsightDataModel.HealingStatsExtension?.HealingPhases?.FirstOrDefault() ?? new HealingPhase();

            var gw2Players = GetGw2Players(eliteInsightDataModel, fightPhase, healingPhase, eliteInsightDataModel.BarrierStatsExtension.BarrierPhases.FirstOrDefault());

            var stringDuration = eliteInsightDataModel.EncounterDuration;

            var secondsOfFight = 0;
            if (!string.IsNullOrEmpty(stringDuration))
            {
                try
                {
                    var minutes = Convert.ToInt32(stringDuration.Substring(0, 2));
                    var seconds = Convert.ToInt32(stringDuration.Substring(4, 2));
                    secondsOfFight = minutes * 60 + seconds;
                }
                catch (Exception)
                {
                    Console.WriteLine($"Failed to parse to seconds of fight `{stringDuration}`");
                }
            }

            var currentDateTimeUtc = DateTime.UtcNow;
            var damagePointCap = 10;
            var cleansePointCap = 5;
            var stabPointsCap = 6;
            var healingPointsCap = 4;
            var stripsPointsCap = 3;
            var barrierPointsCap = 3;

            foreach (var account in accounts)
            {
                account.PreviousPoints = account.Points;
            }

            _databaseContext.UpdateRange(accounts);
            _databaseContext.SaveChanges();

            foreach (var player in gw2Players)
            {
                var account = accounts.FirstOrDefault(a => a.Gw2AccountName == player.AccountName);
                if (account == null)
                {
                    continue;
                }

                var totalPoints = 0d;

                var damagePoints = player.Damage / 50000;
                totalPoints += damagePoints > damagePointCap ? damagePointCap : damagePoints;

                var cleansePoints = player.Cleanses / 100;
                totalPoints += cleansePoints > cleansePointCap ? cleansePointCap : cleansePoints;

                var stripPoints = player.Strips / 30;
                totalPoints += stripPoints > stripsPointsCap ? stripsPointsCap : stripPoints;

                var stabMultiplier = secondsOfFight < 30 ? 1 : secondsOfFight / 30;
                var stabPoint = player.StabUpTime / 0.15 * stabMultiplier;
                totalPoints += stabPoint > stabPointsCap ? stabPointsCap : stabPoint;

                var healingPoints = player.Healing / 50000;
                totalPoints += healingPoints > healingPointsCap ? healingPointsCap : healingPoints;

                var barrierPoints = player.Barrier / 40000;
                totalPoints += barrierPoints > barrierPointsCap ? barrierPointsCap : barrierPoints;

                if (totalPoints < 4)
                {
                    totalPoints = 4;
                }
                else if (totalPoints > 12)
                {
                    totalPoints = 12;
                }

                account.Points += Convert.ToDecimal(totalPoints);
                account.AvailablePoints += Convert.ToDecimal(totalPoints);
                account.LastWvwLogDateTime = currentDateTimeUtc;
                _databaseContext.Update(account);
            }

            await _databaseContext.SaveChangesAsync();
        }

        private static List<Gw2Player> GetGw2Players(EliteInsightDataModel data, ArcDpsPhase fightPhase, HealingPhase healingPhase, BarrierPhase barrierPhase)
        {
            if (data.Players == null)
            {
                return new List<Gw2Player>();
            }

            var gw2Players = new List<Gw2Player>();
            var playerIndex = 0;
            foreach (var arcDpsPlayer in data.Players)
            {
                var existingPlayer = gw2Players.FirstOrDefault(s => s.AccountName == arcDpsPlayer.Acc);
                if (existingPlayer == null)
                {
                    gw2Players.Add(new Gw2Player
                    {
                        AccountName = arcDpsPlayer.Acc,
                        Profession = arcDpsPlayer.Profession,
                        CharacterName = arcDpsPlayer.Name,
                        SubGroup = arcDpsPlayer.Group,
                        Damage = fightPhase.DpsStatsTargets[playerIndex].Sum(s => s.FirstOrDefault()),
                        Cleanses = fightPhase.SupportStats[playerIndex].FirstOrDefault() + (fightPhase.SupportStats[playerIndex].Count >= PlayerCleansesIndex + 1 ? fightPhase.SupportStats[playerIndex][PlayerCleansesIndex] : 0),
                        Strips = fightPhase.SupportStats[playerIndex][PlayerStripsIndex],
                        StabUpTime = fightPhase.BoonGenSquadStats[playerIndex].Data?.CheckIndexIsValid(BoonStabDimension1Index, BoonStabDimension2Index) ?? false ? fightPhase.BoonGenSquadStats[playerIndex].Data[BoonStabDimension1Index][BoonStabDimension2Index] : 0,
                        Healing = healingPhase.OutgoingHealingStatsTargets[playerIndex].Sum(s => s.FirstOrDefault()),
                        Barrier = barrierPhase.OutgoingBarrierStats[playerIndex].FirstOrDefault(),
                        DistanceFromTag = fightPhase.GameplayStats[playerIndex][DistanceFromTagIndex],
                        TimesDowned = fightPhase.DefStats[playerIndex][FriendlyDownIndex].Double ?? 0,
                        Interrupts = fightPhase.OffensiveStats[playerIndex][InterruptsIndex]
                    });
                }
                else
                {
                    existingPlayer.Profession = arcDpsPlayer.Profession;
                    existingPlayer.CharacterName = arcDpsPlayer.Name;
                    existingPlayer.SubGroup = arcDpsPlayer.Group;
                    existingPlayer.Damage += fightPhase.DpsStatsTargets[playerIndex].Sum(s => s.FirstOrDefault());
                    existingPlayer.Cleanses += fightPhase.SupportStats[playerIndex].FirstOrDefault() + (fightPhase.SupportStats[playerIndex].Count >= PlayerCleansesIndex + 1 ? fightPhase.SupportStats[playerIndex][PlayerCleansesIndex] : 0);
                    existingPlayer.Strips += fightPhase.SupportStats[playerIndex][PlayerStripsIndex];
                    existingPlayer.StabUpTime += fightPhase.BoonGenSquadStats[playerIndex].Data?.CheckIndexIsValid(BoonStabDimension1Index, BoonStabDimension2Index) ?? false ? fightPhase.BoonGenSquadStats[playerIndex].Data[BoonStabDimension1Index][BoonStabDimension2Index] : 0;
                    existingPlayer.Healing += healingPhase.OutgoingHealingStatsTargets[playerIndex].Sum(s => s.FirstOrDefault());
                    existingPlayer.Barrier = barrierPhase.OutgoingBarrierStats[playerIndex].FirstOrDefault();
                    existingPlayer.DistanceFromTag = fightPhase.GameplayStats[playerIndex][DistanceFromTagIndex];
                    existingPlayer.TimesDowned += fightPhase.DefStats[playerIndex][FriendlyDownIndex].Double ?? 0;
                    existingPlayer.Interrupts = fightPhase.OffensiveStats[playerIndex][InterruptsIndex];
                }

                playerIndex++;
            }

            return gw2Players;
        }

        public static string GetJokeFooter(int index = -1)
        {
            var footerMessageVariants = new[]
            {
                "What do you like to tank on?",
                "Alexa - make me a Discord bot.",
                "Yes, we raid on EVERY Thursday.",
                "You are doing great, Kaye! - Squirrel",
                "You're right, Logan! - Squirrel",
                "No one on the left cata!",
                "Do your job!",
                "They were ALL interrupted",
                "Cave farm poppin' off",
                "I never lose gay chicken - Aten",
                "It's almost down, 80%"
            };

            return index == -1 ?
                footerMessageVariants[new Random().Next(0, footerMessageVariants.Length)] :
                footerMessageVariants[Math.Min(index, footerMessageVariants.Length)];
        }
    }
}