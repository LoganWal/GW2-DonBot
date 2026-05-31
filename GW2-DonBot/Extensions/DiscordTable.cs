using System.Text;

namespace DonBot.Extensions;

/// <summary>
/// Renders monospace tables for Discord code blocks. Headers and rows are built from the
/// same <see cref="Column"/> specs, so they always line up. Every table is padded to
/// <see cref="MaxRowWidth"/> by spreading any slack across the gaps between columns, so all
/// tables render at the same width regardless of how many columns they have. Trailing spaces are
/// trimmed, keeping rows narrow enough to avoid Discord wrapping the last column onto its own line
/// on mobile.
/// </summary>
public static class DiscordTable
{
    /// <summary>
    /// The width (in characters) every rendered row is padded to. This is also the cap: Discord's
    /// code blocks wrap around this width, so staying at it keeps each row on a single line while
    /// giving every table the same width. The embed width-spacer is sized to match (see
    /// FooterService), so columns padded to this width don't wrap.
    /// </summary>
    public const int MaxRowWidth = 40;

    public enum Align
    {
        Left,
        Right
    }

    public readonly record struct Column(string Header, int Width, Align Align = Align.Left);

    /// <summary>Builds the header line (with trailing newline) from the column headers.</summary>
    public static string Header(IReadOnlyList<Column> columns) =>
        Compose(columns, columns.Select(c => c.Header).ToArray());

    /// <summary>Builds a data row (with trailing newline). Cells are matched to columns by order.</summary>
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

            // Left-aligned (text) columns are clipped to their width so an over-long value can't
            // push later columns out of alignment. Right-aligned (numeric) columns are sized to
            // fit their formatted values and are left intact to avoid corrupting a number.
            builder.Append(column.Align == Align.Right
                ? text.PadLeft(column.Width)
                : text.ClipAt(column.Width).PadRight(column.Width));
        }

        return $"{builder.ToString().TrimEnd()}\n";
    }

    /// <summary>
    /// Width of each gap between columns. Columns are normally separated by a single space, but any
    /// slack up to <see cref="MaxRowWidth"/> is spread evenly across the gaps so every table renders
    /// at the same width. Falls back to single-space gaps when the columns already fill (or exceed)
    /// the target width, so a table is never narrowed below its natural layout.
    /// </summary>
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
