using Discord;
using DonBot.Services.DiscordRequestServices;

namespace DonBot.Tests.Services.DiscordRequestServices;

public class BestTimesButtonPlacementTests
{
    private static Embed PvE(string suffix = "") => new EmbedBuilder().WithTitle($"PvE Overview{suffix}").Build();
    private static Embed Other(string title = "WvW Overview") => new EmbedBuilder().WithTitle(title).Build();

    // --- No button cases ---

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

    // --- Button always on last message ---

    [Fact]
    public void BestTimesTargetIndex_WhenSinglePveEmbed_ReturnsZero()
    {
        var result = RaidCommandCommandService.BestTimesTargetIndex([PvE()]);
        Assert.Equal(0, result);
    }

    [Fact]
    public void BestTimesTargetIndex_WhenPveIsFirstOfTwo_ReturnsLastIndex()
    {
        // PvE at [0], Other at [1] — button must go to index 1, not 0
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
        // Typical raid output: WvW summary, PvE summary, player standings
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

    // --- Correctness: NOT on first PvE message (regression for the old behaviour) ---

    [Fact]
    public void BestTimesTargetIndex_WhenPveIsNotLast_DoesNotReturnFirstPveIndex()
    {
        // Old code returned 0 (first PvE). New code must return last index (2).
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
