using DonBot.Api.Services;

namespace DonBot.Tests.Services.ApiServices;

public sealed class GuildRouteParserTests
{
    [Fact]
    public void TryParse_ValidDiscordSnowflake_ReturnsLongAndUlongValues()
    {
        Assert.True(GuildRouteParser.TryParse("415441457151737870", out var parsed));

        Assert.Equal(415441457151737870L, parsed.Value);
        Assert.Equal(415441457151737870UL, parsed.UnsignedValue);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("abc")]
    public void TryParse_InvalidString_ReturnsFalse(string? input)
    {
        Assert.False(GuildRouteParser.TryParse(input, out _));
    }

    [Fact]
    public void TryParse_ValueAboveLongMax_ReturnsFalse()
    {
        Assert.False(GuildRouteParser.TryParse(ulong.MaxValue.ToString(), out _));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void TryNormalize_NonPositiveRouteValue_ReturnsFalse(long guildId)
    {
        Assert.False(GuildRouteParser.TryNormalize(guildId, out _));
    }

    [Fact]
    public void TryNormalize_PositiveRouteValue_ReturnsValues()
    {
        Assert.True(GuildRouteParser.TryNormalize(42, out var parsed));

        Assert.Equal(42, parsed.Value);
        Assert.Equal(42UL, parsed.UnsignedValue);
    }
}
