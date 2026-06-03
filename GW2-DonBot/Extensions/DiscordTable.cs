using System.Text;

namespace DonBot.Extensions;

/// <summary>Renders fixed-width rows for Discord code block tables.</summary>
public static class DiscordTable
{
    /// <summary>Width cap used to keep table rows from wrapping in Discord embeds.</summary>
    public const int MaxRowWidth = 40;

    public enum Align
    {
        Left,
        Right
    }

    public readonly record struct Column(string Header, int Width, Align Align = Align.Left);

    public static string Header(IReadOnlyList<Column> columns) =>
        Compose(columns, columns.Select(c => c.Header).ToArray());

    public static string Row(IReadOnlyList<Column> columns, params string[] cells) =>
        Compose(columns, cells);

    private static string Compose(IReadOnlyList<Column> columns, IReadOnlyList<string> cells)
    {
        var gaps = GapWidths(columns);
        var builder = new StringBuilder();
        for (var i = 0; i < columns.Count; i++)
        {
            if (i > 0)
            {
                builder.Append(' ', gaps[i - 1]);
            }

            var text = i < cells.Count ? cells[i] ?? string.Empty : string.Empty;
            var column = columns[i];

            // Clip text columns, but leave right-aligned numeric values intact.
            builder.Append(column.Align == Align.Right
                ? text.PadLeft(column.Width)
                : text.ClipAt(column.Width).PadRight(column.Width));
        }

        return $"{builder.ToString().TrimEnd()}\n";
    }

    private static int[] GapWidths(IReadOnlyList<Column> columns)
    {
        var gapCount = columns.Count - 1;
        if (gapCount <= 0)
        {
            return [];
        }

        var gaps = new int[gapCount];
        Array.Fill(gaps, 1);

        var contentWidth = columns.Sum(c => c.Width);
        var slack = MaxRowWidth - contentWidth - gapCount;
        if (slack <= 0)
        {
            return gaps;
        }

        var perGap = slack / gapCount;
        var remainder = slack % gapCount;
        for (var i = 0; i < gapCount; i++)
        {
            gaps[i] += perGap + (i < remainder ? 1 : 0);
        }

        return gaps;
    }
}
