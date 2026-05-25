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

        var expected = $"{"Bob".PadRight(6)} {"10".PadLeft(5)} {"2".PadLeft(4)}";
        Assert.Equal(expected, row);
    }

    [Fact]
    public void Row_TrimsTrailingSpacesFromLastColumn()
    {
        var columns = new[] { new DiscordTable.Column("A", 3), new DiscordTable.Column("B", 5) };
        var row = DiscordTable.Row(columns, "x", "y").TrimEnd('\n');

        // Last column is left-aligned width 5 but trailing padding is trimmed away
        Assert.Equal("x   y", row);
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
