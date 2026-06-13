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

        // Gap widths vary to keep rows within Discord's mobile code-block width.
        Assert.StartsWith("Bob".PadRight(6), row);
        Assert.EndsWith($"{"10",5}{new string(' ', RowGap(Columns, 1))}{"2",4}", row);
    }

    [Fact]
    public void Row_PadsTableToMaxRowWidth()
    {
        var row = DiscordTable.Row(Columns, "Bob", "10", "2").TrimEnd('\n');

        // Right-aligned final columns keep the full uniform row width.
        Assert.Equal(DiscordTable.MaxRowWidth, row.Length);
    }

    [Fact]
    public void Row_TrimsTrailingSpacesFromLastColumn()
    {
        var columns = new[] { new DiscordTable.Column("A", 3), new DiscordTable.Column("B", 5) };
        var row = DiscordTable.Row(columns, "x", "y").TrimEnd('\n');

        // Left-aligned final columns trim their trailing padding.
        var gap = RowGap(columns, 0);
        Assert.Equal($"{"x",-3}{new string(' ', gap)}y", row);
    }

    // Mirrors DiscordTable.GapWidths.
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

        Assert.Equal(header.IndexOf("Dmg", StringComparison.Ordinal) + "Dmg".Length,
            row.IndexOf("10", StringComparison.Ordinal) + "10".Length);
    }
}
