using System.Globalization;
using Discord;
using DonBot.Core.Models.Entities;
using DonBot.Core.Models.Enums;
using DonBot.Core.Models.GuildWars2;
using DonBot.Extensions;
using DonBot.Services.DatabaseServices;
using Microsoft.Extensions.Configuration;

namespace DonBot.Services.GuildWarsServices.MessageGeneration;

public sealed class RaidReportService(
    IEntityService entityService,
    IFooterService footerService,
    IWvWFightSummaryService wvWFightSummaryService,
    IConfiguration configuration) : IRaidReportService
{
    // Kept within DiscordTable.MaxRowWidth for Discord mobile embeds.
    internal static readonly DiscordTable.Column[] SurvivabilityColumns =
    [
        new("Player", 13),
        new("Res(s)", 7, DiscordTable.Align.Right),
        new("DmgTkn", 8, DiscordTable.Align.Right),
        new("Down", 4, DiscordTable.Align.Right),
        new("1st", 3, DiscordTable.Align.Right)
    ];

    internal static readonly DiscordTable.Column[] FightsColumns =
    [
        new("Fight", 13),
        new("Best", 8, DiscordTable.Align.Right),
        new("Success", 12),
        new("Cnt", 3, DiscordTable.Align.Right)
    ];

    internal static readonly DiscordTable.Column[] PlayerColumns =
    [
        new("Player", 12),
        new("Dmg", 8),
        new("Cleave", 7),
        new("Alac", 4),
        new("Quick", 5)
    ];

    internal static readonly DiscordTable.Column[] WvWRaidColumns =
    [
        new("Players", 7, DiscordTable.Align.Right),
        new("Downs", 5, DiscordTable.Align.Right),
        new("Kills", 5, DiscordTable.Align.Right),
        new("TmsDwn", 6, DiscordTable.Align.Right),
        new("Deaths", 6, DiscordTable.Align.Right)
    ];

    internal static readonly DiscordTable.Column[] WvWSubColumns =
    [
        new("Sub", 3),
        new("Quick", 5, DiscordTable.Align.Right),
        new("Alac", 5, DiscordTable.Align.Right),
        new("Intrpt", 6, DiscordTable.Align.Right)
    ];

    private static string SurvivabilityHeader => DiscordTable.Header(SurvivabilityColumns);

    public async Task<(List<Embed>? Embeds, string? WebAppUrl)> Generate(FightsReport fightsReport, long guildId)
    {
        var messages = new List<Embed>();
        if (fightsReport.FightsEnd == null)
        {
            return (null, null);
        }

        var fights = (await entityService.FightLog.GetWhereAsync(s => s.GuildId == guildId && s.FightStart >= fightsReport.FightsStart && s.FightStart <= fightsReport.FightsEnd)).OrderBy(s => s.FightStart).ToList();
        return await GetRaidReport(guildId, fights, messages);
    }

    public async Task<(List<Embed>? Embeds, string? WebAppUrl)> GenerateSimpleReply(List<long> fightLogIds, long guildId)
    {
        var messages = new List<Embed>();
        var fights = (await entityService.FightLog.GetWhereAsync(s => fightLogIds.Contains(s.FightLogId))).ToList();

        return await GetRaidReport(guildId, fights, messages);
    }

    public async Task<Embed> GenerateRaidAlert(long guildId)
    {
        var message = new EmbedBuilder
        {
            Title = "RAID STARTING!\n",
            Description = "***GET IN HERE!***\n",
            Color = (Color)System.Drawing.Color.Gold,
            Author = new EmbedAuthorBuilder()
            {
                Name = "GW2-DonBot",
                Url = "https://github.com/LoganWal/GW2-DonBot",
                IconUrl = "https://i.imgur.com/tQ4LD6H.png"
            },
            Footer = new EmbedFooterBuilder()
            {
                Text = $"{await footerService.Generate(guildId)}",
                IconUrl = "https://i.imgur.com/tQ4LD6H.png"
            },
            Timestamp = DateTime.Now
        };

        footerService.AddInviteLink(message);

        return message.Build();
    }

    internal static Gw2Player AggregatePlayerFights(IGrouping<string, PlayerFightLog> groupedPlayerFight)
    {
        var playersFights = groupedPlayerFight.ToList();
        var player = playersFights.First();
        return new Gw2Player
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
            Damage = (long)Math.Round(playersFights.Average(s => (double)s.Damage), 0),
            DamageDownContribution = (long)Math.Round(playersFights.Average(s => (double)s.DamageDownContribution), 0),
            Cleanses = Math.Round(playersFights.Average(s => (double)s.Cleanses), 0),
            Strips = Math.Round(playersFights.Average(s => (double)s.Strips), 0),
            StabOnGroup = Math.Round(Convert.ToDouble(playersFights.Average(s => (float)s.StabGenOnGroup)), 2),
            StabOffGroup = Math.Round(Convert.ToDouble(playersFights.Average(s => (float)s.StabGenOffGroup)), 2),
            Healing = (long)Math.Round(playersFights.Average(s => (double)s.Healing), 0),
            BarrierGenerated = (long)Math.Round(playersFights.Average(s => (double)s.BarrierGenerated), 0),
            DistanceFromTag = Math.Round(Convert.ToDouble(playersFights.Any(s => s.DistanceFromTag < 1100)
                ? playersFights.Where(s => s.DistanceFromTag < 1100).Average(s => s.DistanceFromTag)
                : 0), 2),
            TotalQuick = Math.Round(Convert.ToDouble(playersFights.Average(s => s.QuicknessDuration)), 2),
            TotalAlac = Math.Round(Convert.ToDouble(playersFights.Average(s => s.AlacDuration)), 2)
        };
    }

    internal static string BuildSurvivabilityTable(List<IGrouping<string, PlayerFightLog>> groupedPlayerFights)
    {
        var rows = BuildSurvivabilityRows(groupedPlayerFights);
        return $"```{SurvivabilityHeader}{string.Concat(rows)}```";
    }

    private static IReadOnlyList<string> BuildSurvivabilityRows(List<IGrouping<string, PlayerFightLog>> groupedPlayerFights)
    {
        var allLogs = groupedPlayerFights.SelectMany(g => g).ToList();

        var firstToDieCounts = allLogs
            .GroupBy(pfl => pfl.FightLogId)
            .Where(g => g.Any(pfl => pfl.TimeOfDeath.HasValue))
            .Select(g => g.Where(pfl => pfl.TimeOfDeath.HasValue)
                .OrderBy(pfl => pfl.TimeOfDeath)
                .First().GuildWarsAccountName)
            .GroupBy(name => name)
            .ToDictionary(g => g.Key, g => g.Count());

        return groupedPlayerFights
            .OrderBy(s => s.Sum(d => d.ResurrectionTime))
            .Select(gw2Player => DiscordTable.Row(SurvivabilityColumns,
                gw2Player.FirstOrDefault()?.GuildWarsAccountName.ClipAt(13) ?? string.Empty,
                Math.Round((double)gw2Player.Sum(s => s.ResurrectionTime) / 1000, 3).ToString(CultureInfo.CurrentCulture),
                gw2Player.Sum(s => s.DamageTaken).ToString(CultureInfo.CurrentCulture),
                gw2Player.Sum(s => s.TimesDowned).ToString(CultureInfo.CurrentCulture),
                firstToDieCounts.GetValueOrDefault(gw2Player.Key, 0).ToString(CultureInfo.CurrentCulture)))
            .ToList();
    }

    private static void AddChunkedCodeFenceFields(EmbedBuilder message, string fieldName, string header, IEnumerable<string> rows, int chunkSize = 12)
    {
        var buffer = new List<string>();
        foreach (var row in rows)
        {
            buffer.Add(row);
            if (buffer.Count == chunkSize)
            {
                var value = $"```{header}{string.Concat(buffer)}```";
                message.AddField(x => { x.Name = fieldName; x.Value = value; x.IsInline = false; });
                buffer.Clear();
            }
        }

        if (buffer.Count > 0)
        {
            var value = $"```{header}{string.Concat(buffer)}```";
            message.AddField(x => { x.Name = fieldName; x.Value = value; x.IsInline = false; });
        }
    }

    private async Task<(List<Embed>? Embeds, string? WebAppUrl)> GetRaidReport(long guildId, List<FightLog> fights, List<Embed> messages)
    {
        fights = fights.OrderBy(s => s.FightStart).ToList();
        var fightLogIds = fights.Select(f => f.FightLogId).ToList();
        var playerFights = await entityService.PlayerFightLog.GetWhereAsync(s => fightLogIds.Contains(s.FightLogId));
        var playerFightLogIds = playerFights.Select(p => p.PlayerFightLogId).ToList();
        await entityService.PlayerFightLogMechanic.GetWhereAsync(m => playerFightLogIds.Contains(m.PlayerFightLogId));

        var groupedPlayerFights = playerFights.GroupBy(s => s.GuildWarsAccountName).OrderByDescending(s => s.Sum(d => d.Damage)).ToList();
        var groupedFights = fights.GroupBy(f => f.FightType).OrderBy(f => f.Key).ToList();

        if (!fights.Any() || !playerFights.Any())
        {
            return (null, null);
        }

        var firstFight = fights.First();
        var lastFight = fights.Last();

        var duration = lastFight.FightStart.AddMilliseconds(lastFight.FightDurationInMs) - firstFight.FightStart;
        var durationString = $"{(int)duration.TotalHours} hrs {(int)duration.TotalMinutes % 60} mins {duration.Seconds} secs";

        var wvwFightCount = fights.Count(s => s.FightType == (short)FightTypesEnum.WvW && s.FightType != (short)FightTypesEnum.Unkn);
        var pveFightCount = fights.Count(s => s.FightType != (short)FightTypesEnum.WvW && s.FightType != (short)FightTypesEnum.Unkn);

        if (wvwFightCount > pveFightCount)
        {
            messages.Add(await GenerateWvWRaidReport(durationString, groupedPlayerFights, false, guildId));
            messages.Add(await GenerateWvWRaidReport(durationString, groupedPlayerFights, true, guildId));

        }
        else
        {
            messages.AddRange(await GeneratePvERaidReport(durationString, groupedFights, groupedPlayerFights, fights, guildId));
            var successLogs = await GeneratePvERaidLogReport(durationString, fights, true, guildId);
            if (successLogs != null)
            {
                messages.Add(successLogs);

            }

            var failedLogs = await GeneratePvERaidLogReport(durationString, fights, false, guildId);
            if (failedLogs != null)
            {
                messages.Add(failedLogs);
            }
        }

        if (messages.Count > 0)
        {
            var webAppBaseUrl = configuration["WebApp:BaseUrl"];
            string? webAppUrl = null;
            if (!string.IsNullOrEmpty(webAppBaseUrl))
            {
                var ids = string.Join(",", fightLogIds);
                webAppUrl = $"{webAppBaseUrl}/logs/aggregate?ids={ids}";
            }

            var lastBuilder = messages[^1].ToEmbedBuilder();
            footerService.AddInviteLink(lastBuilder);
            messages[^1] = lastBuilder.Build();

            return (messages, webAppUrl);
        }

        return (messages, null);
    }

    private async Task<Embed> GenerateWvWRaidReport(string durationString, List<IGrouping<string, PlayerFightLog>> groupedPlayerFights, bool advancedLog, long guildId)
    {
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

        var gw2Players = groupedPlayerFights.Select(AggregatePlayerFights).ToList();

        var dataBySub = gw2Players.GroupBy(s => s.SubGroup);

        message.Footer = new EmbedFooterBuilder()
        {
            Text = $"{await footerService.Generate(guildId)}",
            IconUrl = "https://i.imgur.com/tQ4LD6H.png"
        };

        message.Timestamp = DateTime.Now;

        var statTotals = new StatTotals
        {
            TotalStrips = groupedPlayerFights.Select(groupedPlayerFight => groupedPlayerFight.ToList()).Select(values => values.Sum(s => s.Strips)).Sum()
        };

        if (!advancedLog)
        {
            var raidOverview = $"```{DiscordTable.Header(WvWRaidColumns)}{DiscordTable.Row(WvWRaidColumns,
                gw2Players.Count.ToString(CultureInfo.CurrentCulture),
                gw2Players.Sum(s => s.Downs).ToString(CultureInfo.CurrentCulture),
                gw2Players.Sum(s => s.Kills).ToString(CultureInfo.CurrentCulture),
                gw2Players.Sum(s => s.TimesDowned).ToString(CultureInfo.CurrentCulture),
                gw2Players.Sum(s => s.Deaths).ToString(CultureInfo.CurrentCulture))}```";

            message.AddField(x =>
            {
                x.Name = "Raid Overview";
                x.Value = $"{raidOverview}";
                x.IsInline = false;
            });

            var subOverview = $"```{DiscordTable.Header(WvWSubColumns)}";
            foreach (var subData in dataBySub.OrderBy(s => s.Key))
            {
                subOverview += DiscordTable.Row(WvWSubColumns,
                    subData.Key.ToString(CultureInfo.CurrentCulture),
                    Math.Round(subData.Average(s => s.TotalQuick), 2).ToString(CultureInfo.CurrentCulture),
                    Math.Round(subData.Average(s => s.TotalAlac), 2).ToString(CultureInfo.CurrentCulture),
                    subData.Sum(s => s.TimesInterrupted).ToString(CultureInfo.CurrentCulture));
            }
            subOverview += "```";

            message.AddField(x =>
            {
                x.Name = "Sub Overview";
                x.Value = $"{subOverview}";
                x.IsInline = false;
            });
        }

        return await wvWFightSummaryService.GenerateMessage(advancedLog, 10, gw2Players, message, guildId, statTotals);
    }

    private async Task<List<Embed>> GeneratePvERaidReport(string durationString, List<IGrouping<short, FightLog>> groupedFights, List<IGrouping<string, PlayerFightLog>> groupedPlayerFights, List<FightLog> fights, long guildId)
    {
        var color = (Color)System.Drawing.Color.FromArgb(195, 0, 101);
        var author = new EmbedAuthorBuilder
        {
            Name = "GW2-DonBot",
            Url = "https://github.com/LoganWal/GW2-DonBot",
            IconUrl = "https://i.imgur.com/tQ4LD6H.png"
        };
        async Task<EmbedFooterBuilder> Footer() => new() { Text = await footerService.Generate(guildId), IconUrl = "https://i.imgur.com/tQ4LD6H.png" };

        var fightsEmbed = new EmbedBuilder
        {
            Title = "Report (PvE)\n",
            Description = $"**Length:** {durationString}\n",
            Color = color,
            Author = author,
            Footer = await Footer(),
            Timestamp = DateTime.Now
        };

        var allFightLogs = guildId != -1
            ? await entityService.FightLog.GetWhereAsync(s => s.GuildId == guildId)
            : [];

        allFightLogs.RemoveAll(s => fights.Select(d => d.FightLogId).Contains(s.FightLogId));

        var fightRows = new List<string>();
        foreach (var groupedFight in groupedFights)
        {
            var fightsListForType = groupedFight.ToList();
            foreach (var fightTypeFightMode in fightsListForType.GroupBy(s => s.FightMode))
            {
                var bestFight = allFightLogs
                    .Where(s => s.IsSuccess && s.FightType == groupedFight.Key && s.FightMode == fightTypeFightMode.Key)
                    .OrderBy(s => s.FightDurationInMs)
                    .FirstOrDefault(s => s.IsSuccess);

                var fightTypeFightModeList = fightTypeFightMode.ToList();

                var bestFightTime = TimeSpan.FromMilliseconds(bestFight?.FightDurationInMs ?? 0);
                var bestFightTimeString = bestFightTime.Ticks != 0 ? $"{(bestFightTime.Hours * 60) + bestFightTime.Minutes:D2}m:{bestFightTime.Seconds:D2}s" : "None   ";

                var successFights = fightTypeFightModeList.Where(s => s.IsSuccess).ToList();
                var successFightTime = TimeSpan.FromMilliseconds(successFights.Count != 0 ? successFights.Min(s => s.FightDurationInMs) : 0);
                var successFightTimeString = successFightTime.Ticks != 0 ? $"{(successFightTime.Hours * 60) + successFightTime.Minutes:D2}m:{successFightTime.Seconds:D2}s" : "None   ";

                if (successFightTime <= bestFightTime && successFightTime.Ticks != 0)
                {
                    successFightTimeString += " (!)";
                }

                var fightName = $"({fightTypeFightMode.Key.GetFightModeName()}){Enum.GetName(typeof(FightTypesEnum), groupedFight.Key) ?? nameof(FightTypesEnum.Unkn)}".ClipAt(13);
                fightRows.Add(DiscordTable.Row(FightsColumns,
                    fightName,
                    bestFightTimeString.Trim(),
                    successFightTimeString.Trim(),
                    fightTypeFightModeList.Count.ToString(CultureInfo.CurrentCulture)));
            }
        }

        AddChunkedCodeFenceFields(fightsEmbed, "Fights Overview", DiscordTable.Header(FightsColumns), fightRows);

        var playerEmbed = new EmbedBuilder
        {
            Color = color,
            Author = author,
            Footer = await Footer(),
            Timestamp = DateTime.Now
        };

        var playerLineByDmg = new List<Tuple<float, string>>();
        foreach (var groupedPlayerFight in groupedPlayerFights)
        {
            var playerFightsListForType = groupedPlayerFight.ToList();
            var playerFights = fights.Where(f => playerFightsListForType.Select(s => s.FightLogId).Contains(f.FightLogId));

            var totalFightTimeSec = playerFights.Sum(s => s.FightDurationInMs) / 1000f;
            var dps = playerFightsListForType.Sum(s => s.Damage / totalFightTimeSec);
            var playerLine = DiscordTable.Row(PlayerColumns,
                groupedPlayerFight.Key.ClipAt(12),
                dps.FormatNumber(true),
                playerFightsListForType.Sum(s => s.Cleave / totalFightTimeSec).FormatNumber(true),
                Math.Round(playerFightsListForType.Average(s => s.AlacDuration), 1).ToString(CultureInfo.CurrentCulture),
                Math.Round(playerFightsListForType.Average(s => s.QuicknessDuration), 1).ToString(CultureInfo.CurrentCulture));
            playerLineByDmg.Add(new Tuple<float, string>(dps, playerLine));
        }

        AddChunkedCodeFenceFields(playerEmbed, "Player Overview", DiscordTable.Header(PlayerColumns), playerLineByDmg.OrderByDescending(s => s.Item1).Select(t => t.Item2));

        var surviveEmbed = new EmbedBuilder
        {
            Color = color,
            Author = author,
            Footer = await Footer(),
            Timestamp = DateTime.Now
        };

        AddChunkedCodeFenceFields(surviveEmbed, "Survivability Overview", SurvivabilityHeader, BuildSurvivabilityRows(groupedPlayerFights));

        footerService.AddWidthSpacer(fightsEmbed);
        footerService.AddWidthSpacer(playerEmbed);
        footerService.AddWidthSpacer(surviveEmbed);

        return [fightsEmbed.Build(), playerEmbed.Build(), surviveEmbed.Build()];
    }

    internal static List<string> BuildMechanicRows(
        List<PlayerFightLogMechanic> mechanics,
        Dictionary<long, string> playerFightLogIdToAccount)
    {
        var names = OrderedMechanicNames(mechanics);
        return names.Select(name =>
        {
            var totals = MechanicAccountTotals(name, mechanics, playerFightLogIdToAccount);
            var total = totals.Sum(t => t.Total);
            var top = totals.FirstOrDefault();
            var topStr = top.AccountName != null ? $"{top.AccountName.ClipAt(13)} ({top.Total})" : "-";
            return $"{name.ClipAt(18),-18}{string.Empty,2}{total,-6}{string.Empty,2}{topStr}\n";
        }).ToList();
    }

    internal static List<string> OrderedMechanicNames(List<PlayerFightLogMechanic> mechanics) =>
        mechanics
            .Select(m => m.MechanicName)
            .Distinct()
            .OrderBy(n => mechanics.Where(m => m.MechanicName == n).Sum(m => m.MechanicCount))
            .ToList();

    internal static List<(string AccountName, long Total)> MechanicAccountTotals(
        string mechanicName,
        List<PlayerFightLogMechanic> mechanics,
        Dictionary<long, string> playerFightLogIdToAccount) =>
        mechanics
            .Where(m => m.MechanicName == mechanicName && playerFightLogIdToAccount.ContainsKey(m.PlayerFightLogId))
            .GroupBy(m => playerFightLogIdToAccount[m.PlayerFightLogId])
            .Select(g => (AccountName: g.Key, Total: g.Sum(m => m.MechanicCount)))
            .Where(x => x.Total > 0)
            .OrderByDescending(x => x.Total)
            .ToList();

    private async Task<Embed?> GeneratePvERaidLogReport(string durationString, List<FightLog> fights, bool isSuccessLogs, long guildId)
    {
        var fightLogs = fights.Where(s => s.IsSuccess == isSuccessLogs).ToList();
        if (!fightLogs.Any())
        {
            return null;
        }

        if (!isSuccessLogs)
        {
            fightLogs = fightLogs.OrderBy(s => s.FightType).ThenBy(s => s.FightPhase).ThenByDescending(s => s.FightPercent).ToList();
        }

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
            var currentBatch = fightLogs.GetRange(i, Math.Min(12, fightLogs.Count - i));
            var fightUrlOverview = string.Empty;

            foreach (var item in currentBatch)
            {
                var failedPercentageString = !isSuccessLogs ? $"{(item.FightPhase != null ? ($" - P{item.FightPhase}") : string.Empty)} - {item.FightPercent}%" : string.Empty;
                fightUrlOverview += $"{Enum.GetName(typeof(FightTypesEnum), item.FightType)} - {item.FightMode.GetFightModeName()}{failedPercentageString} - {item.Url}\n";
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
            Text = $"{await footerService.Generate(guildId)}",
            IconUrl = "https://i.imgur.com/tQ4LD6H.png"
        };

        message.Timestamp = DateTime.Now;

        return message.Build();
    }
}
