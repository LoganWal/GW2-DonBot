using System.Globalization;
using Discord;
using DonBot.Extensions;
using DonBot.Models.Entities;
using DonBot.Models.Enums;
using DonBot.Services.DatabaseServices;

namespace DonBot.Services.GuildWarsServices.MessageGeneration;

public sealed class WeeklyLeaderboardService(IEntityService entityService, IFooterService footerService) : IWeeklyLeaderboardService
{
    private const int TopN = 20;
    private const string AuthorIconUrl = "https://i.imgur.com/tQ4LD6H.png";

    public async Task<List<Embed>?> GenerateWvW(Guild guild)
    {
        var cutoff = DateTime.UtcNow.AddDays(-7);
        var fights = await entityService.FightLog.GetWhereAsync(f =>
            f.GuildId == guild.GuildId &&
            f.FightStart >= cutoff &&
            f.FightType == (short)FightTypesEnum.WvW);

        if (fights.Count == 0) return null;

        var fightIds = fights.Select(f => f.FightLogId).ToList();
        var playerFights = await entityService.PlayerFightLog.GetWhereAsync(pf => fightIds.Contains(pf.FightLogId));

        if (playerFights.Count == 0) return null;

        var grouped = playerFights.GroupBy(pf => pf.GuildWarsAccountName).ToList();
        var footerText = await footerService.Generate(guild.GuildId);
        var color = System.Drawing.Color.FromArgb(195, 0, 101);
        var description = $"**Week of {cutoff:MMM dd} – {DateTime.UtcNow:MMM dd, yyyy}**\n";

        // Embed 1: offensive stats
        var embed1 = BuildBaseEmbed("WvW Weekly Leaderboard", description, color);
        embed1.AddField("Damage", BuildWvWDamageTable(grouped), false);
        embed1.AddField("Cleanses", BuildWvWSimpleTable("Cleanses", grouped,
            g => (double)g.Sum(s => s.Cleanses),
            v => ((long)v).ToString(CultureInfo.InvariantCulture)), false);
        embed1.AddField("Strips", BuildWvWSimpleTable("Strips", grouped,
            g => (double)g.Sum(s => s.Strips),
            v => ((long)v).ToString(CultureInfo.InvariantCulture)), false);
        embed1.AddField("Stab", BuildWvWStabTable(grouped), false);
        embed1.AddField("Healing", BuildWvWSimpleTable("Healing", grouped,
            g => (double)g.Sum(s => s.Healing),
            v => ((long)v).FormatNumber()), false);
        embed1.Footer = new EmbedFooterBuilder { Text = footerText, IconUrl = AuthorIconUrl };
        embed1.Timestamp = DateTime.Now;

        // Embed 2: defensive/advanced stats
        var embed2 = BuildBaseEmbed("WvW Weekly Leaderboard (Advanced)", description, color);
        embed2.AddField("Barrier", BuildWvWSimpleTable("Barrier Gen", grouped,
            g => (double)g.Sum(s => s.BarrierGenerated),
            v => ((long)v).FormatNumber()), false);
        embed2.AddField("Times Downed", BuildWvWSimpleTable("Times Downed", grouped,
            g => (double)g.Sum(s => s.TimesDowned),
            v => ((long)v).ToString(CultureInfo.InvariantCulture)), false);
        embed2.AddField("Damage Taken", BuildWvWSimpleTable("Dmg Taken", grouped,
            g => (double)g.Sum(s => s.DamageTaken),
            v => ((long)v).FormatNumber()), false);
        embed2.AddField("Kills", BuildWvWSimpleTable("Total Kills", grouped,
            g => (double)g.Sum(s => s.Kills),
            v => ((long)v).ToString(CultureInfo.InvariantCulture)), false);
        embed2.AddField("Distance From Tag", BuildWvWDistanceTable(grouped), false);
        footerService.AddInviteLink(embed2);
        embed2.Footer = new EmbedFooterBuilder { Text = footerText, IconUrl = AuthorIconUrl };
        embed2.Timestamp = DateTime.Now;

        return [embed1.Build(), embed2.Build()];
    }

    public async Task<Embed?> GeneratePvE(Guild guild)
    {
        var cutoff = DateTime.UtcNow.AddDays(-7);
        var fights = await entityService.FightLog.GetWhereAsync(f =>
            f.GuildId == guild.GuildId &&
            f.FightStart >= cutoff &&
            f.FightType != (short)FightTypesEnum.WvW &&
            f.FightType != (short)FightTypesEnum.Unkn);

        if (fights.Count == 0) return null;

        var fightIds = fights.Select(f => f.FightLogId).ToList();
        var playerFights = await entityService.PlayerFightLog.GetWhereAsync(pf => fightIds.Contains(pf.FightLogId));

        if (playerFights.Count == 0) return null;

        var fightDurations = fights.ToDictionary(f => f.FightLogId, f => f.FightDurationInMs);
        var grouped = playerFights.GroupBy(pf => pf.GuildWarsAccountName).ToList();

        var message = BuildBaseEmbed(
            "PvE Weekly Leaderboard",
            $"**Week of {cutoff:MMM dd} – {DateTime.UtcNow:MMM dd, yyyy}**\n",
            System.Drawing.Color.FromArgb(101, 149, 195));

        message.AddField("DPS", BuildPvEDpsTable(grouped, fightDurations), false);
        message.AddField("Cleave DPS", BuildPvECleaveDpsTable(grouped, fightDurations), false);
        message.AddField("Res Time", BuildPvESimpleTable("Avg Res (s)", grouped,
            g => Math.Round(g.Average(s => s.ResurrectionTime) / 1000.0, 3),
            v => v.ToString("F3", CultureInfo.InvariantCulture)), false);
        message.AddField("Damage Taken", BuildPvESimpleTable("Avg Dmg Taken", grouped,
            g => (double)(long)Math.Round(g.Average(s => (double)s.DamageTaken), 0),
            v => ((long)v).FormatNumber()), false);
        message.AddField("Times Downed", BuildPvESimpleTable("Avg Downed", grouped,
            g => Math.Round(g.Average(s => (double)s.TimesDowned), 2),
            v => v.ToString("F2", CultureInfo.InvariantCulture)), false);

        message.Footer = new EmbedFooterBuilder
        {
            Text = await footerService.Generate(guild.GuildId),
            IconUrl = AuthorIconUrl
        };

        footerService.AddInviteLink(message);
        message.Timestamp = DateTime.Now;

        return message.Build();
    }

    public async Task<Embed?> GetPlayerRanks(Guild guild, List<string> accountNames)
    {
        var cutoff = DateTime.UtcNow.AddDays(-7);
        var description = $"**Week of {cutoff:MMM dd} – {DateTime.UtcNow:MMM dd, yyyy}**\n";

        var message = BuildBaseEmbed("Your Weekly Rankings", description, System.Drawing.Color.FromArgb(114, 137, 218));

        var hasAnyData = false;

        if (guild.WvwLeaderboardEnabled)
        {
            var fights = await entityService.FightLog.GetWhereAsync(f =>
                f.GuildId == guild.GuildId &&
                f.FightStart >= cutoff &&
                f.FightType == (short)FightTypesEnum.WvW);

            if (fights.Count > 0)
            {
                var fightIds = fights.Select(f => f.FightLogId).ToList();
                var playerFights = await entityService.PlayerFightLog.GetWhereAsync(pf => fightIds.Contains(pf.FightLogId));
                var grouped = playerFights.GroupBy(pf => pf.GuildWarsAccountName).ToList();
                var total = grouped.Count;

                var wvwRanks = new System.Text.StringBuilder();
                wvwRanks.AppendLine("```");
                wvwRanks.AppendLine($"{"Metric",-16} {"Rank",-8} Value");

                AppendRank(wvwRanks, accountNames,"Damage", grouped, total,
                    g => (double)g.Sum(s => s.Damage),
                    v => ((long)v).FormatNumber());
                AppendRank(wvwRanks, accountNames,"Down Contrib", grouped, total,
                    g => (double)g.Sum(s => s.DamageDownContribution),
                    v => ((long)v).FormatNumber());
                AppendRank(wvwRanks, accountNames,"Cleanses", grouped, total,
                    g => (double)g.Sum(s => s.Cleanses),
                    v => ((long)v).ToString(CultureInfo.InvariantCulture));
                AppendRank(wvwRanks, accountNames,"Strips", grouped, total,
                    g => (double)g.Sum(s => s.Strips),
                    v => ((long)v).ToString(CultureInfo.InvariantCulture));
                AppendRank(wvwRanks, accountNames,"Healing", grouped, total,
                    g => (double)g.Sum(s => s.Healing),
                    v => ((long)v).FormatNumber());
                AppendRank(wvwRanks, accountNames,"Barrier", grouped, total,
                    g => (double)g.Sum(s => s.BarrierGenerated),
                    v => ((long)v).FormatNumber());
                AppendRank(wvwRanks, accountNames,"Times Downed", grouped, total,
                    g => (double)g.Sum(s => s.TimesDowned),
                    v => ((long)v).ToString(CultureInfo.InvariantCulture),
                    ascending: true);
                AppendRank(wvwRanks, accountNames,"Dmg Taken", grouped, total,
                    g => (double)g.Sum(s => s.DamageTaken),
                    v => ((long)v).FormatNumber());
                AppendRank(wvwRanks, accountNames,"Kills", grouped, total,
                    g => (double)g.Sum(s => s.Kills),
                    v => ((long)v).ToString(CultureInfo.InvariantCulture));

                var eligibleForStab = grouped.Where(g => g.Count() >= 10).ToList();
                AppendRank(wvwRanks, accountNames, "Stab S(on)", eligibleForStab, eligibleForStab.Count,
                    g => g.Average(s => (double)s.StabGenOnGroup),
                    v => v.ToString("F2", CultureInfo.InvariantCulture));

                var eligibleForDist = grouped.Where(g => g.Count() >= 10 && g.Any(s => s.DistanceFromTag > 0 && s.DistanceFromTag < 1100)).ToList();
                AppendRank(wvwRanks, accountNames, "Dist From Tag", eligibleForDist, eligibleForDist.Count,
                    g => g.Where(s => s.DistanceFromTag > 0 && s.DistanceFromTag < 1100).Average(s => (double)s.DistanceFromTag),
                    v => v.ToString("F1", CultureInfo.InvariantCulture),
                    ascending: true);

                wvwRanks.Append("```");
                message.AddField("WvW", wvwRanks.ToString(), false);
                hasAnyData = true;
            }
        }

        if (guild.PveLeaderboardEnabled)
        {
            var fights = await entityService.FightLog.GetWhereAsync(f =>
                f.GuildId == guild.GuildId &&
                f.FightStart >= cutoff &&
                f.FightType != (short)FightTypesEnum.WvW &&
                f.FightType != (short)FightTypesEnum.Unkn);

            if (fights.Count > 0)
            {
                var fightIds = fights.Select(f => f.FightLogId).ToList();
                var playerFights = await entityService.PlayerFightLog.GetWhereAsync(pf => fightIds.Contains(pf.FightLogId));
                var fightDurations = fights.ToDictionary(f => f.FightLogId, f => f.FightDurationInMs);
                var grouped = playerFights.GroupBy(pf => pf.GuildWarsAccountName).ToList();
                var total = grouped.Count;

                var pveRanks = new System.Text.StringBuilder();
                pveRanks.AppendLine("```");
                pveRanks.AppendLine($"{"Metric",-16} {"Rank",-8} Value");

                AppendRank(pveRanks, accountNames,"DPS", grouped, total,
                    g =>
                    {
                        var totalSec = Math.Max(g.Sum(pf => fightDurations.GetValueOrDefault(pf.FightLogId, 1)) / 1000f, 1f);
                        return g.Sum(pf => pf.Damage) / totalSec;
                    },
                    v => ((float)v).FormatNumber(true));
                AppendRank(pveRanks, accountNames,"Cleave DPS", grouped, total,
                    g =>
                    {
                        var totalSec = Math.Max(g.Sum(pf => fightDurations.GetValueOrDefault(pf.FightLogId, 1)) / 1000f, 1f);
                        return g.Sum(pf => pf.Cleave) / totalSec;
                    },
                    v => ((float)v).FormatNumber(true));
                AppendRank(pveRanks, accountNames,"Res Time", grouped, total,
                    g => Math.Round(g.Average(s => s.ResurrectionTime) / 1000.0, 3),
                    v => v.ToString("F3", CultureInfo.InvariantCulture));
                AppendRank(pveRanks, accountNames,"Dmg Taken", grouped, total,
                    g => (double)(long)Math.Round(g.Average(s => (double)s.DamageTaken), 0),
                    v => ((long)v).FormatNumber());
                AppendRank(pveRanks, accountNames,"Times Downed", grouped, total,
                    g => Math.Round(g.Average(s => (double)s.TimesDowned), 2),
                    v => v.ToString("F2", CultureInfo.InvariantCulture),
                    ascending: true);

                pveRanks.Append("```");
                message.AddField("PvE", pveRanks.ToString(), false);
                hasAnyData = true;
            }
        }

        if (!hasAnyData) return null;

        message.Footer = new EmbedFooterBuilder
        {
            Text = await footerService.Generate(guild.GuildId),
            IconUrl = AuthorIconUrl
        };
        message.Timestamp = DateTime.Now;

        return message.Build();
    }

    private static void AppendRank(System.Text.StringBuilder sb, List<string> accountNames, string metric, List<IGrouping<string, PlayerFightLog>> grouped, int total, Func<IGrouping<string, PlayerFightLog>, double> selector, Func<double, string> formatter, bool ascending = false)
    {
        var ordered = ascending
            ? grouped.OrderBy(selector).ToList()
            : grouped.OrderByDescending(selector).ToList();

        var rank = ordered.FindIndex(g => accountNames.Any(n => string.Equals(g.Key, n, StringComparison.OrdinalIgnoreCase))) + 1;
        if (rank == 0)
        {
            sb.AppendLine($"{metric,-16} {"N/A",-8}");
            return;
        }

        var value = formatter(selector(ordered[rank - 1]));
        sb.AppendLine($"{metric,-16} #{rank}/{total,-5} {value}");
    }

    private static EmbedBuilder BuildBaseEmbed(string title, string description, System.Drawing.Color color) =>
        new()
        {
            Title = $"{title}\n",
            Description = description,
            Color = (Color)color,
            Author = new EmbedAuthorBuilder
            {
                Name = "GW2-DonBot",
                Url = "https://github.com/LoganWal/GW2-DonBot",
                IconUrl = AuthorIconUrl
            }
        };

    private static string BuildWvWDamageTable(List<IGrouping<string, PlayerFightLog>> grouped)
    {
        var table = "```#    Name                   Damage    Down C\n";
        var index = 1;
        foreach (var g in grouped.OrderByDescending(g => g.Sum(s => s.Damage)).Take(TopN))
        {
            var name = $"({g.Count()}) {g.Key}".ClipAt(21);
            var totalDmg = ((float)g.Sum(s => s.Damage)).FormatNumber();
            var totalDc = ((float)g.Sum(s => s.DamageDownContribution)).FormatNumber();
            table += $"{index.ToString().PadLeft(2, '0')}{string.Empty,3}{name,-21}{string.Empty,2}{totalDmg,-8}{string.Empty,2}{totalDc}\n";
            index++;
        }
        return table + "```";
    }

    private static string BuildWvWStabTable(List<IGrouping<string, PlayerFightLog>> grouped)
    {
        var table = "```#    Name                   S(on)  S(off)\n";
        var index = 1;
        foreach (var g in grouped.Where(g => g.Count() >= 10).OrderByDescending(g => g.Average(s => s.StabGenOnGroup)).Take(TopN))
        {
            var name = $"({g.Count()}) {g.Key}".ClipAt(21);
            var stabOn = Math.Round(g.Average(s => (double)s.StabGenOnGroup), 2).ToString("F2", CultureInfo.InvariantCulture);
            var stabOff = Math.Round(g.Average(s => (double)s.StabGenOffGroup), 2).ToString("F2", CultureInfo.InvariantCulture);
            table += $"{index.ToString().PadLeft(2, '0')}{string.Empty,3}{name,-21}{string.Empty,2}{stabOn,-6}{string.Empty,1}{stabOff}\n";
            index++;
        }
        return table + "```";
    }

    private static string BuildWvWDistanceTable(List<IGrouping<string, PlayerFightLog>> grouped)
    {
        var table = "```#    Name                   Avg Dist\n";
        var index = 1;
        // Filter to players who were actually near the tag in at least some fights (same threshold as per-fight view)
        // Order ascending — lower distance = closer to tag = better
        var eligible = grouped
            .Where(g => g.Count() >= 10 && g.Any(s => s.DistanceFromTag > 0 && s.DistanceFromTag < 1100))
            .OrderBy(g => g.Where(s => s.DistanceFromTag > 0 && s.DistanceFromTag < 1100).Average(s => (double)s.DistanceFromTag))
            .Take(TopN);

        foreach (var g in eligible)
        {
            var name = $"({g.Count()}) {g.Key}".ClipAt(21);
            var avgDist = Math.Round(g.Where(s => s.DistanceFromTag > 0 && s.DistanceFromTag < 1100).Average(s => (double)s.DistanceFromTag), 1);
            table += $"{index.ToString().PadLeft(2, '0')}{string.Empty,3}{name,-21}{string.Empty,2}{avgDist.ToString("F1", CultureInfo.InvariantCulture)}\n";
            index++;
        }
        return table + "```";
    }

    private static string BuildWvWSimpleTable(string columnHeader, List<IGrouping<string, PlayerFightLog>> grouped, Func<IGrouping<string, PlayerFightLog>, double> selector, Func<double, string> formatter, bool ascending = false)
    {
        var table = $"```#    Name                   {columnHeader}\n";
        var ordered = ascending
            ? grouped.OrderBy(selector)
            : grouped.OrderByDescending(selector);
        var index = 1;
        foreach (var g in ordered.Take(TopN))
        {
            var name = $"({g.Count()}) {g.Key}".ClipAt(21);
            table += $"{index.ToString().PadLeft(2, '0')}{string.Empty,3}{name,-21}{string.Empty,2}{formatter(selector(g))}\n";
            index++;
        }
        return table + "```";
    }

    private static string BuildPvEDpsTable(List<IGrouping<string, PlayerFightLog>> grouped, Dictionary<long, long> fightDurations)
    {
        var table = "```#    Name                   Avg DPS\n";
        var players = grouped.Select(g =>
        {
            var totalSec = Math.Max(g.Sum(pf => fightDurations.GetValueOrDefault(pf.FightLogId, 1)) / 1000f, 1f);
            return (Name: $"({g.Count()}) {g.Key}".ClipAt(21), Dps: g.Sum(pf => pf.Damage) / totalSec);
        }).OrderByDescending(p => p.Dps).Take(TopN).ToList();

        var index = 1;
        foreach (var (name, dps) in players)
        {
            table += $"{index.ToString().PadLeft(2, '0')}{string.Empty,3}{name,-21}{string.Empty,2}{dps.FormatNumber(true)}\n";
            index++;
        }
        return table + "```";
    }

    private static string BuildPvECleaveDpsTable(List<IGrouping<string, PlayerFightLog>> grouped, Dictionary<long, long> fightDurations)
    {
        var table = "```#    Name                   Avg Cleave/s\n";
        var players = grouped.Select(g =>
        {
            var totalSec = Math.Max(g.Sum(pf => fightDurations.GetValueOrDefault(pf.FightLogId, 1)) / 1000f, 1f);
            return (Name: $"({g.Count()}) {g.Key}".ClipAt(21), Dps: g.Sum(pf => pf.Cleave) / totalSec);
        }).OrderByDescending(p => p.Dps).Take(TopN).ToList();

        var index = 1;
        foreach (var (name, dps) in players)
        {
            table += $"{index.ToString().PadLeft(2, '0')}{string.Empty,3}{name,-21}{string.Empty,2}{dps.FormatNumber(true)}\n";
            index++;
        }
        return table + "```";
    }

    private static string BuildPvESimpleTable(string columnHeader, List<IGrouping<string, PlayerFightLog>> grouped, Func<IGrouping<string, PlayerFightLog>, double> selector, Func<double, string> formatter, bool ascending = false)
    {
        var table = $"```#    Name                   {columnHeader}\n";
        var ordered = ascending
            ? grouped.OrderBy(selector)
            : grouped.OrderByDescending(selector);
        var index = 1;
        foreach (var g in ordered.Take(TopN))
        {
            var name = $"({g.Count()}) {g.Key}".ClipAt(21);
            table += $"{index.ToString().PadLeft(2, '0')}{string.Empty,3}{name,-21}{string.Empty,2}{formatter(selector(g))}\n";
            index++;
        }
        return table + "```";
    }
}
