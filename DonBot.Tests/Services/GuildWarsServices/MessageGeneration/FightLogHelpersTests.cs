using DonBot.Services.GuildWarsServices.MessageGeneration;

namespace DonBot.Tests.Services.GuildWarsServices.MessageGeneration;

public class FightLogHelpersTests
{
    [Theory]
    [InlineData("https://b.dps.report/abc-def", "b.dps.report")]
    [InlineData("https://dps.report/xyz", "dps.report")]
    [InlineData("https://wvw.report/123", "wvw.report")]
    [InlineData("https://gw2wingman.nevermindcreations.de/log/abc", "gw2wingman.nevermindcreations.de")]
    public void GetLogSource_ValidAbsoluteUri_ReturnsHost(string url, string expectedHost)
    {
        Assert.Equal(expectedHost, FightLogHelpers.GetLogSource(url));
    }

    [Theory]
    [InlineData("not-a-url")]
    [InlineData("relative/path")]
    [InlineData("")]
    public void GetLogSource_NotAnAbsoluteUri_ReturnsUnknown(string url)
    {
        Assert.Equal("unknown", FightLogHelpers.GetLogSource(url));
    }

    [Fact]
    public void GetLogSource_HostIsLowercased()
    {
        // Uri normalises the host to lowercase regardless of input casing
        Assert.Equal("b.dps.report", FightLogHelpers.GetLogSource("https://B.DPS.REPORT/abc"));
    }
}
