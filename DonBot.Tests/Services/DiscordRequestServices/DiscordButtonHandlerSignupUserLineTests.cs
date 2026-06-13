using DonBot.Services.DiscordRequestServices;

namespace DonBot.Tests.Services.DiscordRequestServices;

public sealed class DiscordButtonHandlerSignupUserLineTests
{
    [Fact]
    public void FormatUserLine_WithUsername_AppendsAccountName()
    {
        var line = DiscordButtonHandler.FormatUserLine("<@123>", "TestUser");

        Assert.Equal("<@123> (TestUser)", line);
    }

    [Theory]
    [InlineData("<@123>")]
    [InlineData("<@123> (TestUser)")]
    [InlineData("<@123>(TestUser)")]
    [InlineData("<@!123>")]
    [InlineData("<@!123> (TestUser)")]
    [InlineData("TestUser")]
    public void IsSameUserLine_ForCurrentAndLegacyFormats_MatchesUser(string line)
    {
        var matches = DiscordButtonHandler.IsSameUserLine(line, 123, "TestUser");

        Assert.True(matches);
    }

    [Theory]
    [InlineData("<@1234> (TestUser)")]
    [InlineData("<@12> (TestUser)")]
    [InlineData("OtherUser")]
    public void IsSameUserLine_ForDifferentUser_DoesNotMatch(string line)
    {
        var matches = DiscordButtonHandler.IsSameUserLine(line, 123, "TestUser");

        Assert.False(matches);
    }
}
