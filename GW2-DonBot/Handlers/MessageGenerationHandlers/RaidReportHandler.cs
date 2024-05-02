using Discord;
using Extensions;
using Models;
using Models.Entities;
using Models.Enums;
using Models.Statics;
using System.Globalization;

namespace Handlers.MessageGenerationHandlers
{
    public class RaidReportHandler
    {
        private readonly FooterHandler _footerHandler;
        private readonly DatabaseContext _databaseContext;
        private readonly WvWFightSummaryHandler _wvWFightSummaryHandler;

        public RaidReportHandler(FooterHandler footerHandler, DatabaseContext databaseContext, WvWFightSummaryHandler wvWFightSummaryHandler)
        {
            _footerHandler = footerHandler;
            _databaseContext = databaseContext;
            _wvWFightSummaryHandler = wvWFightSummaryHandler;
        }

        public List<Embed>? Generate(FightsReport fightsReport, long guildId)
        {
            var messages = new List<Embed>();
            if (fightsReport.FightsEnd == null)
            {
                return null;
            }

            var fights = _databaseContext.FightLog.Where(s => s.GuildId == guildId && s.FightStart >= fightsReport.FightsStart && s.FightStart <= fightsReport.FightsEnd).OrderBy(s => s.FightStart).ToList();
            var playerFights = _databaseContext.PlayerFightLog.ToList(); 
            playerFights = playerFights.Where(s => fights.Select(f => f.FightLogId).Contains(s.FightLogId)).ToList();
            var groupedPlayerFights = playerFights.GroupBy(s => s.GuildWarsAccountName).OrderByDescending(s => s.Sum(d => d.Damage)).ToList();
            var groupedFights = fights.GroupBy(f => f.FightType).OrderBy(f => f.Key).ToList();

            if (!fights.Any() || !playerFights.Any())
            {
                return null;
            }

            var firstFight = fights.First();
            var lastFight = fights.Last();

            var duration = lastFight.FightStart.AddMilliseconds(lastFight.FightDurationInMs) - firstFight.FightStart;
            var durationString = $"{(int)duration.TotalHours} hrs {(int)duration.TotalMinutes % 60} mins {duration.Seconds} secs";

            var wvwFightCount = fights.Count(s => s.FightType == (short)FightTypesEnum.WvW && s.FightType != (short)FightTypesEnum.unkn);
            var pveFightCount = fights.Count(s => s.FightType != (short)FightTypesEnum.WvW && s.FightType != (short)FightTypesEnum.unkn);

            if (wvwFightCount > pveFightCount)
            {
                messages.Add(GenerateWvWRaidReport(durationString, groupedPlayerFights, false));
                messages.Add(GenerateWvWRaidReport(durationString, groupedPlayerFights, true));

            }
            else
            {
                messages.Add(GeneratePvERaidReport(durationString, groupedFights, groupedPlayerFights, fights));
                var successLogs = GeneratePvERaidLogReport(durationString, fights, true);
                if (successLogs != null)
                {
                    messages.Add(successLogs);

                }

                var failedLogs = GeneratePvERaidLogReport(durationString, fights, false);
                if (failedLogs != null)
                {
                    messages.Add(failedLogs);
                }
            }

            return messages;
        }

        public Embed GenerateRaidAlert()
        {
            // Building the message via embeds
            var message = new EmbedBuilder
            {
                Title = "RAID STARTING!\n",
                Description = $"***GET IN HERE!***\n",
                Color = (Color)System.Drawing.Color.Gold,
                Author = new EmbedAuthorBuilder()
                {
                    Name = "GW2-DonBot",
                    Url = "https://github.com/LoganWal/GW2-DonBot",
                    IconUrl = "https://i.imgur.com/tQ4LD6H.png"
                },
                Footer = new EmbedFooterBuilder()
                {
                    Text = $"{_footerHandler.Generate()}",
                    IconUrl = "https://i.imgur.com/tQ4LD6H.png"
                },
                Timestamp = DateTime.Now
            };

            return message.Build();
        }

        private Embed GenerateWvWRaidReport(string durationString, List<IGrouping<string, PlayerFightLog>> groupedPlayerFights, bool advancedLog)
        {
            // Building the message via embeds
            var message = new EmbedBuilder
            {
                Title = "Report (WvW)\n",
                Description = $"**Length:** {durationString}\n",
                Color = (Color)System.Drawing.Color.FromArgb(195, 0, 101),
                Author = new EmbedAuthorBuilder()
                {
                    Name = "GW2-DonBot",
                    Url = "https://github.com/LoganWal/GW2-DonBot",
                    IconUrl = "https://i.imgur.com/tQ4LD6H.png"
                }
            };

            var gw2Players = new List<Gw2Player>();
            foreach (var groupedPlayerFight in groupedPlayerFights)
            {
                var playersFights = groupedPlayerFight.ToList();
                var player = playersFights.First();
                gw2Players.Add(new Gw2Player
                {
                    AccountName = $"({playersFights.Count}) {player.GuildWarsAccountName}",
                    SubGroup = playersFights.GroupBy(s => s.SubGroup).MaxBy(s => s.Count())?.Key ?? player.SubGroup,
                    Kills = playersFights.Sum(s => s.Kills),
                    Downs = playersFights.Sum(s => s.Downs),
                    TimesDowned = playersFights.Sum(s => s.TimesDowned),
                    Deaths = playersFights.Sum(s => s.Deaths),
                    Interrupts = playersFights.Sum(s => s.Interrupts),
                    NumberOfHitsWhileBlinded = playersFights.Sum(s => s.NumberOfHitsWhileBlinded),
                    NumberOfMissesAgainst = playersFights.Sum(s => s.NumberOfMissesAgainst),
                    NumberOfTimesBlockedAttack = playersFights.Sum(s => s.NumberOfTimesBlockedAttack),
                    NumberOfTimesEnemyBlockedAttack = playersFights.Sum(s => s.NumberOfTimesEnemyBlockedAttack),
                    NumberOfBoonsRipped = playersFights.Sum(s => s.NumberOfBoonsRipped),
                    DamageTaken = playersFights.Sum(s => s.DamageTaken),
                    BarrierMitigation = playersFights.Sum(s => s.BarrierMitigation),
                    TimesInterrupted = playersFights.Sum(s => s.TimesInterrupted),
                    Damage = (long)Math.Round(playersFights.Any(s => s.Damage > 0)
                        ? playersFights.Where(s => s.Damage > 0).Average(s => s.Damage)
                        : 0, 0),
                    DamageDownContribution = (long)Math.Round(playersFights.Any(s => s.DamageDownContribution > 0)
                        ? playersFights.Where(s => s.DamageDownContribution > 0).Average(s => s.DamageDownContribution)
                        : 0, 0),
                    Cleanses = Math.Round(playersFights.Any(s => s.Cleanses > 0)
                        ? playersFights.Where(s => s.Cleanses > 0).Average(s => s.Cleanses)
                        : 0, 0),
                    Strips = Math.Round(playersFights.Any(s => s.Strips > 0)
                        ? playersFights.Where(s => s.Strips > 0).Average(s => s.Strips)
                        : 0, 0),
                    StabUpTime = Math.Round(Convert.ToDouble(playersFights.Any(s => s.StabGenerated > 0)
                        ? playersFights.Where(s => s.StabGenerated > 0).Average(s => s.StabGenerated)
                        : 0), 2),
                    Healing = (long)Math.Round(playersFights.Any(s => s.Healing > 0)
                        ? playersFights.Where(s => s.Healing > 0).Average(s => s.Healing)
                        : 0, 0),
                    BarrierGenerated = (long)Math.Round(playersFights.Any(s => s.BarrierGenerated > 0)
                        ? playersFights.Where(s => s.BarrierGenerated > 0).Average(s => s.BarrierGenerated)
                        : 0, 0),
                    DistanceFromTag = Math.Round(Convert.ToDouble(playersFights.Any(s => s.DistanceFromTag < 1100)
                        ? playersFights.Where(s => s.DistanceFromTag < 1100).Average(s => s.DistanceFromTag)
                        : 0), 2),
                    TotalQuick = Math.Round(Convert.ToDouble(playersFights.Any(s => s.QuicknessDuration > 0)
                        ? playersFights.Where(s => s.QuicknessDuration > 0).Average(s => s.QuicknessDuration)
                        : 0), 2),
                    TotalAlac = Math.Round(Convert.ToDouble(playersFights.Any(s => s.AlacDuration > 0)
                        ? playersFights.Where(s => s.AlacDuration > 0).Average(s => s.AlacDuration)
                        : 0), 2)
                });
            }

            var dataBySub = gw2Players.GroupBy(s => s.SubGroup);

            message.Footer = new EmbedFooterBuilder()
            {
                Text = $"{_footerHandler.Generate()}",
                IconUrl = "https://i.imgur.com/tQ4LD6H.png"
            };

            // Timestamp
            message.Timestamp = DateTime.Now;

            var statTotals = new StatTotals
            {
                TotalStrips = groupedPlayerFights.Select(groupedPlayerFight => groupedPlayerFight.ToList()).Select(values => values.Sum(s => s.Strips)).Sum()
            };

            if (!advancedLog)
            {
                // raid overview
                var raidOverview = "```Players   Downs   Kills   Times Downed   Deaths\n";
                raidOverview += $"{gw2Players.Count,-4}{string.Empty,-6}{gw2Players.Sum(s => s.Downs), -4}{string.Empty,-4}{gw2Players.Sum(s => s.Kills), -4}{string.Empty,-4}{gw2Players.Sum(s => s.TimesDowned), -4}{string.Empty,-11}{gw2Players.Sum(s => s.Deaths), -4}```";

                message.AddField(x =>
                {
                    x.Name = "Raid Overview";
                    x.Value = $"{raidOverview}";
                    x.IsInline = false;
                });

                var subOverview = "```Sub   Quick   Alac    Interrupted\n";
                foreach (var subData in dataBySub.OrderBy(s => s.Key))
                {
                    subOverview += $"{subData.Key,-2}{string.Empty,-4}{Math.Round(subData.Average(s => s.TotalQuick), 2),-5}{string.Empty,-3}{Math.Round(subData.Average(s => s.TotalAlac), 2),-5}{string.Empty,-3}{subData.Sum(s => s.TimesInterrupted),-5}\n";
                }
                subOverview += "```";

                message.AddField(x =>
                {
                    x.Name = "Sub Overview";
                    x.Value = $"{subOverview}";
                    x.IsInline = false;
                });
            }
            
            // Building the message for use
            return _wvWFightSummaryHandler.GenerateMessage(advancedLog, 10, gw2Players, message, statTotals);
        }

        private Embed GeneratePvERaidReport(string durationString, List<IGrouping<short, FightLog>> groupedFights, List<IGrouping<string, PlayerFightLog>> groupedPlayerFights, List<FightLog> fights)
        {
            // Building the message via embeds
            var message = new EmbedBuilder
            {
                Title = "Report (PvE)\n",
                Description = $"**Length:** {durationString}\n",
                Color = (Color)System.Drawing.Color.FromArgb(195, 0, 101),
                Author = new EmbedAuthorBuilder()
                {
                    Name = "GW2-DonBot",
                    Url = "https://github.com/LoganWal/GW2-DonBot",
                    IconUrl = "https://i.imgur.com/tQ4LD6H.png"
                }
            };

            var fightsOverview = "```Fight       Total (t)    Success (t)     Count\n";
            foreach (var groupedFight in groupedFights)
            {
                var fightsListForType = groupedFight.ToList();

                var fightTime = TimeSpan.FromMilliseconds(fightsListForType.Sum(s => s.FightDurationInMs));
                var fightTimeString = $"{(fightTime.Hours * 60) + fightTime.Minutes:D2}m:{fightTime.Seconds:D2}s";

                var successFightTime = TimeSpan.FromMilliseconds(fightsListForType.Where(s => s.IsSuccess).Sum(s => s.FightDurationInMs));
                var successFightTimeString = $"{(successFightTime.Hours * 60) + successFightTime.Minutes:D2}m:{successFightTime.Seconds:D2}s";

                fightsOverview += $"{Enum.GetName(typeof(FightTypesEnum), groupedFight.Key)?.PadRight(10) ?? "uknwn"}{string.Empty,2}{fightTimeString,-6}{string.Empty,6}{successFightTimeString,-6}{string.Empty,9}{fightsListForType.Count}\n";
            }

            fightsOverview += "```";

            var playerOverview = "```Player           Dmg       Alac    Quick\n";
            foreach (var groupedPlayerFight in groupedPlayerFights)
            {
                var playerFightsListForType = groupedPlayerFight.ToList();
                var playerFights = fights.Where(f => playerFightsListForType.Select(s => s.FightLogId).Contains(f.FightLogId));

                var totalFightTimeSec = (float)(playerFights.Sum(s => s.FightDurationInMs) / 1000f);
                playerOverview += $"{groupedPlayerFight.Key?.ClipAt(13),-13}{string.Empty,4}{playerFightsListForType.Sum(s => (float)s.Damage / totalFightTimeSec).FormatNumber(true),-8}{string.Empty,2}{Math.Round(playerFightsListForType.Average(s => s.AlacDuration), 2).ToString(CultureInfo.CurrentCulture),-5}{string.Empty,3}{Math.Round(playerFightsListForType.Average(s => s.QuicknessDuration), 2).ToString(CultureInfo.CurrentCulture),-5}\n";
            }

            playerOverview += "```";

            message.AddField(x =>
            {
                x.Name = "Fights Overview";
                x.Value = $"{fightsOverview}";
                x.IsInline = false;
            });

            message.AddField(x =>
            {
                x.Name = "Player Overview";
                x.Value = $"{playerOverview}";
                x.IsInline = false;
            });

            foreach (var groupedFight in groupedFights)
            {
                var mechanicsOverview = string.Empty;

                if (groupedFight.Key == (short)FightTypesEnum.ToF)
                {
                    mechanicsOverview = GenerateMechanicsOverview((short)FightTypesEnum.ToF, "```Player           Orbs   Spreads   P1 Dmg\n", pf => pf.CerusPhaseOneDamage, groupedPlayerFights, fights);
                }

                if (groupedFight.Key == (short)FightTypesEnum.Deimos)
                {
                    mechanicsOverview = GenerateMechanicsOverview((short)FightTypesEnum.Deimos, "```Player           Oils\n", pf => pf.DeimosOilsTriggered, groupedPlayerFights, fights);
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

        private string GenerateMechanicsOverview(short fightType, string header, Func<PlayerFightLog, decimal> orderBySelector, IEnumerable<IGrouping<string, PlayerFightLog>> groupedPlayerFights, List<FightLog> fights)
        {
            var mechanicsOverview = header;

            foreach (var groupedPlayerFight in groupedPlayerFights.OrderByDescending(group => group.Sum(orderBySelector)))
            {
                var playerFightsListForType = groupedPlayerFight.ToList();
                var playerFights = fights.Where(f => playerFightsListForType.Select(s => s.FightLogId).Contains(f.FightLogId)).ToList();
                playerFights = playerFights.Where(s => s.FightType == fightType).ToList();
                playerFightsListForType = playerFightsListForType.Where(s => playerFights.Select(s => s.FightLogId).Contains(s.FightLogId)).ToList();

                if (playerFightsListForType.Any())
                {
                    if (fightType == (short)FightTypesEnum.ToF)
                    {
                        mechanicsOverview += $"{playerFightsListForType.FirstOrDefault()?.GuildWarsAccountName.ClipAt(13),-13}{string.Empty,4}{playerFightsListForType.Sum(s => s.CerusOrbsCollected),-3}{string.Empty,4}{playerFightsListForType.Sum(s => s.CerusSpreadHitCount),-3}{string.Empty,7}{((float)playerFightsListForType.Max(s => s.CerusPhaseOneDamage)).FormatNumber(true),-8}\n";
                    }
                    else if (fightType == (short)FightTypesEnum.Deimos)
                    {
                        mechanicsOverview += $"{playerFightsListForType.FirstOrDefault()?.GuildWarsAccountName.ClipAt(13),-13}{string.Empty,4}{playerFightsListForType.Sum(s => s.DeimosOilsTriggered),-3}\n";
                    }
                }
            }

            return mechanicsOverview + "```";
        }
        private Embed? GeneratePvERaidLogReport(string durationString, List<FightLog> fights, bool isSuccessLogs)
        {
            var fightLogs = fights.Where(s => s.IsSuccess == isSuccessLogs).ToList();
            if (!fightLogs.Any())
            {
                return null;
            }

            // Building the message via embeds
            var message = new EmbedBuilder
            {
                Title = $"{(isSuccessLogs ? "Success" : "Failed")} Report (PvE)\n",
                Description = $"**Length:** {durationString}",
                Color = isSuccessLogs ? Color.Green : Color.Red,
                Author = new EmbedAuthorBuilder()
                {
                    Name = "GW2-DonBot",
                    Url = "https://github.com/LoganWal/GW2-DonBot",
                    IconUrl = "https://i.imgur.com/tQ4LD6H.png"
                }
            };

            for (var i = 0; i < fightLogs.Count; i += 12)
            {
                // Process the current batch (from index 'i' to a maximum of 'i + 20')
                var currentBatch = fightLogs.GetRange(i, Math.Min(12, fightLogs.Count - i));
                var fightUrlOverview = string.Empty;

                foreach (var item in currentBatch)
                {
                    var failedPercentageString = !isSuccessLogs ? $"- {item.FightPercent}" : string.Empty;
                    fightUrlOverview += $"{Enum.GetName(typeof(FightTypesEnum), item.FightType)}{failedPercentageString} - {item.Url}\n";
                }

                message.AddField(x =>
                {
                    x.Name = "Fight Logs";
                    x.Value = $"{fightUrlOverview}";
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
