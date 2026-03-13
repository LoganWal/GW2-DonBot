using Discord;
using DonBot.Services.DiscordRequestServices;

namespace DonBot.Tests.Services.DiscordRequestServices;

public class BestTimesButtonPlacementTests
{
    private static Embed PvE(string suffix = "") => new EmbedBuilder().WithTitle($"PvE Overview{suffix}").Build();
    private static Embed Other(string title = "WvW Overview") => new EmbedBuilder().WithTitle(title).Build();

    [Fact]
    public void BestTimesTargetIndex_WhenListIsEmpty_ReturnsNull()
    {
        var result = RaidCommandCommandService.BestTimesTargetIndex([]);
        Assert.Null(result);
    }

    [Fact]
    public void BestTimesTargetIndex_WhenNoPveEmbed_ReturnsNull()
    {
        var result = RaidCommandCommandService.BestTimesTargetIndex([Other(), Other("Summary")]);
        Assert.Null(result);
    }

    [Fact]
    public void BestTimesTargetIndex_WhenSingleNonPveEmbed_ReturnsNull()
    {
        var result = RaidCommandCommandService.BestTimesTargetIndex([Other()]);
        Assert.Null(result);
    }

    [Fact]
    public void BestTimesTargetIndex_WhenSinglePveEmbed_ReturnsZero()
    {
        var result = RaidCommandCommandService.BestTimesTargetIndex([PvE()]);
        Assert.Equal(0, result);
    }

    [Fact]
    public void BestTimesTargetIndex_WhenPveIsFirstOfTwo_ReturnsLastIndex()
    {
        var result = RaidCommandCommandService.BestTimesTargetIndex([PvE(), Other()]);
        Assert.Equal(1, result);
    }

    [Fact]
    public void BestTimesTargetIndex_WhenPveIsLastOfTwo_ReturnsLastIndex()
    {
        var result = RaidCommandCommandService.BestTimesTargetIndex([Other(), PvE()]);
        Assert.Equal(1, result);
    }

    [Fact]
    public void BestTimesTargetIndex_WhenPveIsMiddleOfThree_ReturnsLastIndex()
    {
        var result = RaidCommandCommandService.BestTimesTargetIndex([Other("WvW"), PvE(), Other("Standings")]);
        Assert.Equal(2, result);
    }

    [Fact]
    public void BestTimesTargetIndex_WithManyEmbeds_AlwaysReturnsLastIndex()
    {
        var messages = new List<Embed> { Other(), Other(), PvE(), Other(), Other() };
        var result = RaidCommandCommandService.BestTimesTargetIndex(messages);
        Assert.Equal(messages.Count - 1, result);
    }

    [Fact]
    public void BestTimesTargetIndex_WhenPveIsNotLast_DoesNotReturnFirstPveIndex()
    {
        // Regression: old code returned 0 (first PvE); must return last index (2)
        var result = RaidCommandCommandService.BestTimesTargetIndex([PvE(), Other(), Other()]);
        Assert.NotEqual(0, result);
        Assert.Equal(2, result);
    }

    [Fact]
    public void BestTimesTargetIndex_WithMultiplePveEmbeds_ReturnsLastIndex()
    {
        var result = RaidCommandCommandService.BestTimesTargetIndex([PvE("A"), PvE("B"), Other()]);
        Assert.Equal(2, result);
    }
}
