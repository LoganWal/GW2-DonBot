using DonBot.Extensions;

namespace DonBot.Tests.Extensions;

public class ListExtensionsTests
{
    [Fact]
    public void CheckIndexIsValid_BothIndicesInRange_ReturnsTrue()
    {
        var list = new List<List<double>> { new() { 1.0, 2.0 }, new() { 3.0, 4.0 } };
        Assert.True(list.CheckIndexIsValid(1, 1));
    }

    [Fact]
    public void CheckIndexIsValid_OuterIndexOutOfRange_ReturnsFalse()
    {
        var list = new List<List<double>> { new() { 1.0 } };
        Assert.False(list.CheckIndexIsValid(5, 0));
    }

    [Fact]
    public void CheckIndexIsValid_InnerIndexOutOfRange_ReturnsFalse()
    {
        var list = new List<List<double>> { new() { 1.0 } };
        Assert.False(list.CheckIndexIsValid(0, 5));
    }

    [Fact]
    public void CheckIndexIsValid_OuterIndexEqualsCount_ReturnsFalse()
    {
        // boundary: index == count is invalid (count is one past the last valid index)
        var list = new List<List<double>> { new() { 1.0 } };
        Assert.False(list.CheckIndexIsValid(1, 0));
    }

    [Fact]
    public void CheckIndexIsValid_InnerIndexEqualsInnerCount_ReturnsFalse()
    {
        var list = new List<List<double>> { new() { 1.0, 2.0 } };
        Assert.False(list.CheckIndexIsValid(0, 2));
    }

    [Fact]
    public void CheckIndexIsValid_EmptyOuter_ReturnsFalse()
    {
        var list = new List<List<double>>();
        Assert.False(list.CheckIndexIsValid(0, 0));
    }

    [Fact]
    public void CheckIndexIsValid_EmptyInner_AnyInnerIndexReturnsFalse()
    {
        var list = new List<List<double>> { new() };
        Assert.False(list.CheckIndexIsValid(0, 0));
    }
}
