using DonBot.Services.GuildWarsServices;

namespace DonBot.Tests.Services.GuildWarsServices;

public class PendingLogServiceTests
{
    private static PendingLogState MakeState(ulong uploaderId = 123UL) =>
        new(["https://b.dps.report/abc"], GuildId: 1L, UploaderId: uploaderId);

    [Fact]
    public void StorePending_ReturnsNonEmptyKey()
    {
        var svc = new PendingLogService();
        var key = svc.StorePending(MakeState());
        Assert.False(string.IsNullOrEmpty(key));
    }

    [Fact]
    public void StorePending_Returns16CharKey()
    {
        var svc = new PendingLogService();
        var key = svc.StorePending(MakeState());
        Assert.Equal(16, key.Length);
    }

    [Fact]
    public void StorePending_ReturnsDifferentKeysForDifferentCalls()
    {
        var svc = new PendingLogService();
        var key1 = svc.StorePending(MakeState());
        var key2 = svc.StorePending(MakeState());
        Assert.NotEqual(key1, key2);
    }

    [Fact]
    public void TryConsume_WithValidKey_ReturnsStoredState()
    {
        var svc = new PendingLogService();
        var state = MakeState(uploaderId: 42UL);
        var key = svc.StorePending(state);

        var result = svc.TryConsume(key);

        Assert.NotNull(result);
        Assert.Equal(state.UploaderId, result.UploaderId);
        Assert.Equal(state.GuildId, result.GuildId);
        Assert.Equal(state.Urls, result.Urls);
    }

    [Fact]
    public void TryConsume_WithUnknownKey_ReturnsNull()
    {
        var svc = new PendingLogService();
        var result = svc.TryConsume("doesnotexist");
        Assert.Null(result);
    }

    [Fact]
    public void TryConsume_CanOnlyBeCalledOncePerKey()
    {
        var svc = new PendingLogService();
        var key = svc.StorePending(MakeState());

        var first = svc.TryConsume(key);
        var second = svc.TryConsume(key);

        Assert.NotNull(first);
        Assert.Null(second);
    }

    [Fact]
    public void TryConsume_TwoSeparateKeys_EachReturnCorrectState()
    {
        var svc = new PendingLogService();
        var state1 = new PendingLogState(["https://b.dps.report/aaa"], 10L, 100UL);
        var state2 = new PendingLogState(["https://b.dps.report/bbb"], 20L, 200UL);

        var key1 = svc.StorePending(state1);
        var key2 = svc.StorePending(state2);

        var result1 = svc.TryConsume(key1);
        var result2 = svc.TryConsume(key2);

        Assert.Equal(100UL, result1?.UploaderId);
        Assert.Equal(200UL, result2?.UploaderId);
    }

    [Fact]
    public void TryConsume_AfterConsuming_OtherKeysUnaffected()
    {
        var svc = new PendingLogService();
        var key1 = svc.StorePending(MakeState());
        var key2 = svc.StorePending(MakeState(uploaderId: 999UL));

        svc.TryConsume(key1); // consume first

        var result2 = svc.TryConsume(key2);
        Assert.NotNull(result2);
        Assert.Equal(999UL, result2.UploaderId);
    }
}
