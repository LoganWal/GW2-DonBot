using DonBot.Services.GuildWarsServices.MessageGeneration;

namespace DonBot.Tests.Services.GuildWarsServices.MessageGeneration;

public sealed class WvWFightSummaryServiceTests
{
    [Theory]
    [InlineData(1_000, 10, 100)]
    [InlineData(1_000, 0, 0)]
    [InlineData(1_000, -1, 0)]
    public void CalculateDps_ReturnsFiniteValue(long damage, float durationSeconds, float expected)
    {
        var result = WvWFightSummaryService.CalculateDps(damage, durationSeconds);

        Assert.Equal(expected, result);
        Assert.True(float.IsFinite(result));
    }

    [Theory]
    [InlineData(21264.416015625, "21264")]
    [InlineData(2539.582275390625, "2540")]
    public void FormatDistance_RoundsToWholeNumber(double distance, string expected)
    {
        Assert.Equal(expected, WvWFightSummaryService.FormatDistance(distance));
    }
}
