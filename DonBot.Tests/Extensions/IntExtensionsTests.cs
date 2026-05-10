using DonBot.Extensions;

namespace DonBot.Tests.Extensions;

public class IntExtensionsTests
{
    [Theory]
    [InlineData(0, "NM")]
    [InlineData(1, "CM")]
    [InlineData(2, "LCM")]
    public void GetFightModeName_KnownModes_ReturnsExpectedShortName(int mode, string expected)
    {
        Assert.Equal(expected, mode.GetFightModeName());
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(3)]
    [InlineData(99)]
    public void GetFightModeName_UnknownMode_FallsBackToNm(int mode)
    {
        Assert.Equal("NM", mode.GetFightModeName());
    }
}
