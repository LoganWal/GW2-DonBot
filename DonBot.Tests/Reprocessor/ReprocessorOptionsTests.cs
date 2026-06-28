using DonBot.Reprocessor;

namespace DonBot.Tests.Reprocessor;

public class ReprocessorOptionsTests
{
    [Fact]
    public void TryParseBackfillPlaytypesEnablesOnlyPlaytypeBackfill()
    {
        var parsed = ReprocessorOptions.TryParse(
            ["--backfill-playtypes", "--batch-size", "250", "--from-id", "10", "--to-id", "99"],
            out var options,
            out var error);

        Assert.True(parsed);
        Assert.Null(error);
        Assert.True(options.BackfillPlaytypes);
        Assert.False(options.AwardMissingPoints);
        Assert.False(options.BackfillUraProgress);
        Assert.False(options.BackfillHtProgress);
        Assert.Equal(250, options.BatchSize);
        Assert.Equal(10, options.FromId);
        Assert.Equal(99, options.ToId);
    }

    [Fact]
    public void TryParseAllEnablesAllRegisteredReprocessors()
    {
        var parsed = ReprocessorOptions.TryParse(["--all", "--dry-run", "--force"], out var options, out var error);

        Assert.True(parsed);
        Assert.Null(error);
        Assert.True(options.BackfillPlaytypes);
        Assert.True(options.AwardMissingPoints);
        Assert.True(options.BackfillUraProgress);
        Assert.True(options.BackfillHtProgress);
        Assert.True(options.DryRun);
        Assert.True(options.Force);
    }

    [Theory]
    [InlineData("--backfill-ura-progress")]
    [InlineData("--update-ura-progress")]
    public void TryParseBackfillUraProgressEnablesOnlyUraProgressBackfill(string optionName)
    {
        var parsed = ReprocessorOptions.TryParse([optionName], out var options, out var error);

        Assert.True(parsed);
        Assert.Null(error);
        Assert.False(options.BackfillPlaytypes);
        Assert.False(options.AwardMissingPoints);
        Assert.True(options.BackfillUraProgress);
        Assert.False(options.BackfillHtProgress);
        Assert.True(options.HasWork);
    }

    [Theory]
    [InlineData("--backfill-ht-progress")]
    [InlineData("--update-ht-progress")]
    public void TryParseBackfillHtProgressEnablesOnlyHtProgressBackfill(string optionName)
    {
        var parsed = ReprocessorOptions.TryParse([optionName], out var options, out var error);

        Assert.True(parsed);
        Assert.Null(error);
        Assert.False(options.BackfillPlaytypes);
        Assert.False(options.AwardMissingPoints);
        Assert.False(options.BackfillUraProgress);
        Assert.True(options.BackfillHtProgress);
        Assert.True(options.HasWork);
    }

    [Fact]
    public void TryParseRejectsInvalidIdRange()
    {
        var parsed = ReprocessorOptions.TryParse(["--backfill-playtypes", "--from-id", "100", "--to-id", "10"], out _, out var error);

        Assert.False(parsed);
        Assert.Equal("--from-id cannot be greater than --to-id.", error);
    }
}
