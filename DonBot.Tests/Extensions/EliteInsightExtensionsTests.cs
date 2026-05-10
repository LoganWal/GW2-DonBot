using DonBot.Extensions;

namespace DonBot.Tests.Extensions;

public class EliteInsightExtensionsTests
{
    [Fact]
    public void GetClassAppend_NullClass_ReturnsEmpty()
    {
        Assert.Equal(string.Empty, EliteInsightExtensions.GetClassAppend(null));
    }

    [Fact]
    public void GetClassAppend_EmptyClass_ReturnsEmpty()
    {
        Assert.Equal(string.Empty, EliteInsightExtensions.GetClassAppend(string.Empty));
    }

    [Theory]
    [InlineData("Guardian", " (Grd)")]
    [InlineData("Firebrand", " (Fb)")]
    [InlineData("Willbender", " (Wlb)")]
    [InlineData("Mesmer", " (Msm)")]
    [InlineData("Virtuoso", " (Vrt)")]
    [InlineData("Soulbeast", " (Slb)")]
    [InlineData("Reaper", " (Rpr)")]
    [InlineData("Catalyst", " (Cat)")]
    [InlineData("Mechanist", " (Mec)")]
    public void GetClassAppend_KnownClass_ReturnsParenthesisedShorthand(string className, string expected)
    {
        Assert.Equal(expected, EliteInsightExtensions.GetClassAppend(className));
    }

    [Fact]
    public void GetClassAppend_UnknownClass_ReturnsThreeQuestionMarks()
    {
        Assert.Equal(" (???)", EliteInsightExtensions.GetClassAppend("DefinitelyNotAClass"));
    }

    [Fact]
    public void GetClassAppend_CaseSensitive_LowercaseClassNotMatched()
    {
        // dictionary lookup is case-sensitive: lowercase is treated as unknown
        Assert.Equal(" (???)", EliteInsightExtensions.GetClassAppend("guardian"));
    }
}
