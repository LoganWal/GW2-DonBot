using DonBot.Api.Services;

namespace DonBot.Tests.Services.Api;

public class TusFileMappingTests
{
    [Fact]
    public void TryRemove_UnknownKey_ReturnsFalseAndZero()
    {
        var map = new TusFileMapping();
        Assert.False(map.TryRemove("missing", out var id));
        Assert.Equal(0L, id);
    }

    [Fact]
    public void Add_ThenTryRemove_ReturnsStoredId()
    {
        var map = new TusFileMapping();
        map.Add("tus-1", 42L);

        Assert.True(map.TryRemove("tus-1", out var id));
        Assert.Equal(42L, id);
    }

    [Fact]
    public void TryRemove_ConsumesEntry()
    {
        var map = new TusFileMapping();
        map.Add("tus-1", 42L);

        map.TryRemove("tus-1", out _);

        Assert.False(map.TryRemove("tus-1", out _));
    }

    [Fact]
    public void Add_SameKeyTwice_OverwritesValue()
    {
        var map = new TusFileMapping();
        map.Add("tus-1", 1L);
        map.Add("tus-1", 2L);

        Assert.True(map.TryRemove("tus-1", out var id));
        Assert.Equal(2L, id);
    }

    [Fact]
    public void Add_DifferentKeys_IndependentEntries()
    {
        var map = new TusFileMapping();
        map.Add("tus-1", 1L);
        map.Add("tus-2", 2L);

        map.TryRemove("tus-1", out var id1);
        map.TryRemove("tus-2", out var id2);

        Assert.Equal(1L, id1);
        Assert.Equal(2L, id2);
    }
}
