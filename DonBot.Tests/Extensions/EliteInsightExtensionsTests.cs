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
    [InlineData("Guardian", " (Gua)")]
    [InlineData("Firebrand", " (Fir)")]
    [InlineData("Willbender", " (Wil)")]
    [InlineData("Mesmer", " (Mes)")]
    [InlineData("Virtuoso", " (Vir)")]
    [InlineData("Soulbeast", " (Sou)")]
    [InlineData("Reaper", " (Rea)")]
    [InlineData("Catalyst", " (Cat)")]
    [InlineData("Mechanist", " (Mec)")]
    public void GetClassAppend_KnownClass_ReturnsParenthesisedFirstThreeCharacters(string className, string expected)
    {
        Assert.Equal(expected, EliteInsightExtensions.GetClassAppend(className));
    }

    [Fact]
    public void GetClassAppend_ClassShorterThanThreeCharacters_ReturnsWholeName()
    {
        Assert.Equal(" (Ax)", EliteInsightExtensions.GetClassAppend("Ax"));
    }

    [Fact]
    public void GetClassAppend_PreservesOriginalCasing()
    {
        Assert.Equal(" (gua)", EliteInsightExtensions.GetClassAppend("guardian"));
    }
}
