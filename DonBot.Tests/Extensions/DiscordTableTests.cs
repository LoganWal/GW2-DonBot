using DonBot.Extensions;

namespace DonBot.Tests.Extensions;

public class DiscordTableTests
{
    private static readonly DiscordTable.Column[] Columns =
    [
        new("Name", 6),
        new("Dmg", 5, DiscordTable.Align.Right),
        new("Hits", 4, DiscordTable.Align.Right)
    ];

    [Fact]
    public void Header_UsesColumnHeadersInOrder()
    {
        var header = DiscordTable.Header(Columns);
        Assert.StartsWith("Name", header);
        Assert.Contains("Dmg", header);
        Assert.Contains("Hits", header);
    }

    [Fact]
    public void HeaderAndRow_EndWithNewline()
    {
        Assert.EndsWith("\n", DiscordTable.Header(Columns));
        Assert.EndsWith("\n", DiscordTable.Row(Columns, "Bob", "10", "2"));
    }

    [Fact]
    public void Row_LeftAlignsFirstColumnAndRightAlignsNumeric()
    {
        var row = DiscordTable.Row(Columns, "Bob", "10", "2").TrimEnd('\n');

        // First column left-aligned (padded right) to its width, numeric columns right-aligned.
        // Gaps are widened to pad the table to MaxRowWidth, so match on the column content rather
        // than on a fixed single-space separator.
        Assert.StartsWith("Bob".PadRight(6), row);
        Assert.EndsWith($"{"10".PadLeft(5)}{new string(' ', RowGap(Columns, 1))}{"2".PadLeft(4)}", row);
    }

    [Fact]
    public void Row_PadsTableToMaxRowWidth()
    {
        var row = DiscordTable.Row(Columns, "Bob", "10", "2").TrimEnd('\n');

        // The row ends in a right-aligned column (no trailing padding to trim), so it spans the full
        // uniform width: every table renders at MaxRowWidth regardless of its natural column widths.
        Assert.Equal(DiscordTable.MaxRowWidth, row.Length);
    }

    [Fact]
    public void Row_TrimsTrailingSpacesFromLastColumn()
    {
        var columns = new[] { new DiscordTable.Column("A", 3), new DiscordTable.Column("B", 5) };
        var row = DiscordTable.Row(columns, "x", "y").TrimEnd('\n');

        // Last column is left-aligned, so its trailing padding (and any gap slack that lands after
        // its content) is trimmed away, leaving the value flush against the widened gap.
        var gap = RowGap(columns, 0);
        Assert.Equal($"{"x".PadRight(3)}{new string(' ', gap)}y", row);
    }

    // Mirrors DiscordTable.GapWidths: one space per gap plus an even share of the slack needed to
    // reach MaxRowWidth, with the remainder going to the leftmost gaps.
    private static int RowGap(IReadOnlyList<DiscordTable.Column> columns, int gapIndex)
    {
        var gapCount = columns.Count - 1;
        var contentWidth = columns.Sum(c => c.Width);
        var slack = DiscordTable.MaxRowWidth - contentWidth - gapCount;
        if (slack <= 0)
        {
            return 1;
        }

        return 1 + slack / gapCount + (gapIndex < slack % gapCount ? 1 : 0);
    }

    [Fact]
    public void HeaderColumnPositionsMatchRowColumnPositions()
    {
        var header = DiscordTable.Header(Columns).TrimEnd('\n');
        var row = DiscordTable.Row(Columns, "Bob", "10", "2").TrimEnd('\n');

        // Right-aligned numeric columns line up on their trailing edge in both header and row
        Assert.Equal(header.IndexOf("Dmg", StringComparison.Ordinal) + "Dmg".Length,
            row.IndexOf("10", StringComparison.Ordinal) + "10".Length);
    }
}
