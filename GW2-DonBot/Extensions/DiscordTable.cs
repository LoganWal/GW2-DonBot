using System.Text;

namespace DonBot.Extensions;

/// <summary>
/// Renders monospace tables for Discord code blocks. Headers and rows are built from the
/// same <see cref="Column"/> specs, so they always line up. Columns are separated by a single
/// space and trailing spaces are trimmed, keeping rows narrow enough to avoid Discord wrapping
/// the last column onto its own line on mobile.
/// </summary>
public static class DiscordTable
{
    /// <summary>
    /// Target maximum width (in characters) for a rendered row. Discord's mobile code blocks
    /// wrap around this width; staying at or under it keeps each row on a single line.
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
        var builder = new StringBuilder();
        for (var i = 0; i < columns.Count; i++)
        {
            if (i > 0)
            {
                builder.Append(' ');
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
}
