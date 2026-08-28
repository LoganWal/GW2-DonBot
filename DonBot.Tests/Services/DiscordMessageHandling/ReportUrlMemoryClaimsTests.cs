using DonBot.Services.DiscordServices;

namespace DonBot.Tests.Services.DiscordMessageHandling;

public sealed class ReportUrlMemoryClaimsTests
{
    [Fact]
    public async Task TryClaim_ConcurrentAttemptsAllowOnlyOneOwner()
    {
        var claims = new ReportUrlMemoryClaims();
        const string reportUrl = "https://wvw.report/LD84-20260828-204103_wvw";

        var results = await Task.WhenAll(
            Enumerable.Range(0, 20)
                .Select(_ => Task.Run(() => claims.TryClaim(reportUrl))));

        Assert.Single(results, claimed => claimed);
    }

    [Fact]
    public void Release_AllowsRetryAfterFailureBeforeDelivery()
    {
        var claims = new ReportUrlMemoryClaims();
        const string reportUrl = "https://wvw.report/LD84-20260828-204103_wvw";

        Assert.True(claims.TryClaim(reportUrl));
        claims.Release(reportUrl);

        Assert.True(claims.TryClaim(reportUrl));
    }
}
