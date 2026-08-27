using System.Globalization;
using Discord;
using DonBot.Core.Models.Entities;
using DonBot.Extensions;
using DonBot.Services.DatabaseServices;

namespace DonBot.Services.GuildWarsServices.MessageGeneration;

public sealed class PlayerPointRankingService(IEntityService entityService, IFooterService footerService)
    : IPlayerPointRankingService
{
    public const string EmbedTitle = "Report - WvW points";
    private const int BatchSize = 20;
    private const int TotalRankingLimit = 50;
    private const string AuthorIconUrl = "https://i.imgur.com/tQ4LD6H.png";

    internal static readonly DiscordTable.Column[] LatestFightColumns =
    [
        new("#", 3),
        new("Name", 23),
        new("Points", 10, DiscordTable.Align.Right)
    ];

    internal static readonly DiscordTable.Column[] TotalPointsColumns =
    [
        new("#", 3),
        new("Name", 21),
        new("Points", 12, DiscordTable.Align.Right)
    ];

    public async Task<IReadOnlyList<Embed>> Generate(Guild guild, long fightLogId)
    {
        var awards = await entityService.PlayerPointAward.GetWhereAsync(a => a.FightLogId == fightLogId);
        var latestRows = awards
            .GroupBy(a => a.DiscordId)
            .Select(group => new RankingRow(
                group.Key,
                group.OrderBy(a => a.GuildWarsAccountName, StringComparer.OrdinalIgnoreCase)
                    .First().GuildWarsAccountName,
                group.Sum(a => a.Points)))
            .OrderByDescending(row => row.Points)
            .ThenBy(row => row.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var guildWarsAccounts = (await entityService.GuildWarsAccount.GetAllAsync())
            .Where(account => !string.IsNullOrWhiteSpace(account.GuildWarsAccountName))
            .GroupBy(account => account.DiscordId)
            .ToDictionary(
                group => group.Key,
                group => group.Select(account => account.GuildWarsAccountName!)
                    .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                    .First());

        var totalRows = (await entityService.Account.GetAllAsync())
            .Where(account => guildWarsAccounts.ContainsKey(account.DiscordId))
            .OrderByDescending(account => account.Points)
            .ThenBy(account => guildWarsAccounts[account.DiscordId], StringComparer.OrdinalIgnoreCase)
            .Take(TotalRankingLimit)
            .Select(account => new RankingRow(
                account.DiscordId,
                guildWarsAccounts[account.DiscordId],
                account.Points))
            .ToList();

        var fight = await entityService.FightLog.GetFirstOrDefaultAsync(f => f.FightLogId == fightLogId);
        var footer = await footerService.Generate(guild.GuildId);

        var latestEmbed = BuildBaseEmbed("**WvW Last fight points:**\n", fight?.Url);
        AddBatches(
            latestEmbed,
            "Latest Fight Points",
            LatestFightColumns,
            latestRows,
            row => $"+{FormatPoints(row.Points)}");
        CompleteEmbed(latestEmbed, footer);

        var totalEmbed = BuildBaseEmbed("**WvW total points:**\n");
        AddBatches(
            totalEmbed,
            "Total Points",
            TotalPointsColumns,
            totalRows,
            row => FormatTotalPoints(row.Points));
        CompleteEmbed(totalEmbed, footer);

        return [latestEmbed.Build(), totalEmbed.Build()];
    }

    private static EmbedBuilder BuildBaseEmbed(string description, string? url = null) => new()
    {
        Title = EmbedTitle,
        Description = description,
        Url = url,
        Color = (Color)System.Drawing.Color.FromArgb(230, 231, 232),
        Author = new EmbedAuthorBuilder
        {
            Name = "GW2-DonBot",
            Url = "https://github.com/LoganWal/GW2-DonBot",
            IconUrl = AuthorIconUrl
        }
    };

    private static void AddBatches(
        EmbedBuilder embed,
        string fieldName,
        IReadOnlyList<DiscordTable.Column> columns,
        IReadOnlyList<RankingRow> rows,
        Func<RankingRow, string> pointsCell)
    {
        for (var offset = 0; offset < rows.Count; offset += BatchSize)
        {
            var table = $"```{DiscordTable.Header(columns)}";
            for (var index = offset; index < Math.Min(offset + BatchSize, rows.Count); index++)
            {
                var row = rows[index];
                table += DiscordTable.Row(
                    columns,
                    (index + 1).ToString("000", CultureInfo.InvariantCulture),
                    row.Name,
                    pointsCell(row));
            }
            table += "```";
            embed.AddField(fieldName, table);
        }
    }

    private void CompleteEmbed(EmbedBuilder embed, string footer)
    {
        embed.Footer = new EmbedFooterBuilder
        {
            Text = footer,
            IconUrl = AuthorIconUrl
        };
        footerService.AddInviteLink(embed);
        footerService.AddWidthSpacer(embed);
        embed.Timestamp = DateTime.Now;
    }

    private static string FormatPoints(decimal points) =>
        points.ToString("0.###", CultureInfo.InvariantCulture);

    private static string FormatTotalPoints(decimal total)
    {
        var rounded = Math.Round(total, 0, MidpointRounding.AwayFromZero);
        var value = rounded.ToString("N0", CultureInfo.InvariantCulture);
        if (value.Length <= TotalPointsColumns[^1].Width)
        {
            return value;
        }

        return FormatCompactPoints(rounded).ClipAt(TotalPointsColumns[^1].Width);
    }

    private static string FormatCompactPoints(decimal points)
    {
        var absolute = Math.Abs(points);
        return absolute switch
        {
            >= 1_000_000_000_000m => $"{(points / 1_000_000_000_000m).ToString("0", CultureInfo.InvariantCulture)}T",
            >= 1_000_000_000m => $"{(points / 1_000_000_000m).ToString("0", CultureInfo.InvariantCulture)}B",
            >= 1_000_000m => $"{(points / 1_000_000m).ToString("0", CultureInfo.InvariantCulture)}M",
            >= 1_000m => $"{(points / 1_000m).ToString("0", CultureInfo.InvariantCulture)}K",
            _ => points.ToString("N0", CultureInfo.InvariantCulture)
        };
    }

    private sealed record RankingRow(long DiscordId, string Name, decimal Points);
}
