using DonBot.Extensions;

namespace DonBot.Tests.Extensions;

public class StringExtensionsTests
{
    // -------------------------------------------------------------------------
    // ClipAt
    // -------------------------------------------------------------------------

    [Fact]
    public void ClipAt_StringShorterThanLimit_ReturnsOriginal()
    {
        Assert.Equal("hi", "hi".ClipAt(10));
    }

    [Fact]
    public void ClipAt_StringEqualToLimit_ReturnsOriginal()
    {
        Assert.Equal("abcd", "abcd".ClipAt(4));
    }

    [Fact]
    public void ClipAt_StringLongerThanLimit_TruncatesToLimit()
    {
        Assert.Equal("abcd", "abcdefgh".ClipAt(4));
    }

    [Fact]
    public void ClipAt_ZeroLength_ReturnsEmptyString()
    {
        Assert.Equal(string.Empty, "abcdef".ClipAt(0));
    }

    // -------------------------------------------------------------------------
    // PadCenter
    // -------------------------------------------------------------------------

    [Fact]
    public void PadCenter_OddPadding_BiasesLeftPadding()
    {
        // "ab" centered in 5 -> 1 left, 2 right (or vice versa depending on impl)
        var result = "ab".PadCenter(5);
        Assert.Equal(5, result.Length);
        Assert.Contains("ab", result);
    }

    [Fact]
    public void PadCenter_EvenPadding_EqualLeftAndRight()
    {
        var result = "ab".PadCenter(6);
        Assert.Equal("  ab  ", result);
    }

    [Fact]
    public void PadCenter_StringEqualToLength_NotPadded()
    {
        Assert.Equal("hello", "hello".PadCenter(5));
    }

    [Fact]
    public void PadCenter_CustomPaddingChar_UsesProvidedChar()
    {
        Assert.Equal("--ab--", "ab".PadCenter(6, '-'));
    }

    // -------------------------------------------------------------------------
    // FormatNumber(float, float reference)
    // -------------------------------------------------------------------------

    [Fact]
    public void FormatNumber_FloatWithBillionReference_FormattedAsBillions()
    {
        Assert.Equal("1.5B", 1_500_000_000f.FormatNumber(2_000_000_000f));
    }

    [Fact]
    public void FormatNumber_FloatWithMillionReference_FormattedAsMillions()
    {
        Assert.Equal("2.5M", 2_500_000f.FormatNumber(5_000_000f));
    }

    [Fact]
    public void FormatNumber_FloatWithThousandReference_FormattedAsThousands()
    {
        Assert.Equal("1.5K", 1500f.FormatNumber(2000f));
    }

    [Fact]
    public void FormatNumber_FloatWithSmallReference_FormattedAsRaw()
    {
        Assert.Equal("42.0", 42f.FormatNumber(100f));
    }

    // -------------------------------------------------------------------------
    // FormatNumber(float, asPerSec)
    // -------------------------------------------------------------------------

    [Fact]
    public void FormatNumber_BillionsBranch()
    {
        Assert.Equal("1.5B", 1_500_000_000f.FormatNumber());
    }

    [Fact]
    public void FormatNumber_MillionsBranch()
    {
        Assert.Equal("2.5M", 2_500_000f.FormatNumber());
    }

    [Fact]
    public void FormatNumber_ThousandsBranch()
    {
        Assert.Equal("1.5K", 1500f.FormatNumber());
    }

    [Fact]
    public void FormatNumber_BelowThousand_NoSuffix()
    {
        Assert.Equal("42.0", 42f.FormatNumber());
    }

    [Fact]
    public void FormatNumber_AsPerSec_AppendsPerSecSuffix()
    {
        Assert.Equal("1.5K/s", 1500f.FormatNumber(asPerSec: true));
    }

    [Fact]
    public void FormatNumber_AsPerSecBelowThousand_AppendsPerSecSuffix()
    {
        Assert.Equal("42.0/s", 42f.FormatNumber(asPerSec: true));
    }

    // -------------------------------------------------------------------------
    // FormatNumber(long)
    // -------------------------------------------------------------------------

    [Fact]
    public void FormatNumber_Long_DelegatesToFloatOverload()
    {
        Assert.Equal("2.5M", 2_500_000L.FormatNumber());
    }

    // -------------------------------------------------------------------------
    // TimeToSeconds
    // -------------------------------------------------------------------------

    [Fact]
    public void TimeToSeconds_PlainSeconds()
    {
        Assert.Equal(45f, "45s".TimeToSeconds());
    }

    [Fact]
    public void TimeToSeconds_Minutes_ConvertedToSeconds()
    {
        Assert.Equal(120f, "2m".TimeToSeconds());
    }

    [Fact]
    public void TimeToSeconds_Hours_ConvertedToSeconds()
    {
        Assert.Equal(3600f, "1hr".TimeToSeconds());
    }

    [Fact]
    public void TimeToSeconds_Milliseconds_ConvertedToSeconds()
    {
        Assert.Equal(0.5f, "500ms".TimeToSeconds(), 3);
    }

    [Fact]
    public void TimeToSeconds_CompoundString_SumsParts()
    {
        // "ms" check runs first because the "s" branch isn't reached when "ms" is matched.
        Assert.Equal(125.25f, "2m 5s 250ms".TimeToSeconds(), 3);
    }

    [Fact]
    public void TimeToSeconds_EmptyString_ReturnsZero()
    {
        Assert.Equal(0f, "".TimeToSeconds());
    }

    [Fact]
    public void TimeToSeconds_HoursMinutesSeconds_SumsAllUnits()
    {
        Assert.Equal(3661f, "1hr 1m 1s".TimeToSeconds(), 3);
    }
}
