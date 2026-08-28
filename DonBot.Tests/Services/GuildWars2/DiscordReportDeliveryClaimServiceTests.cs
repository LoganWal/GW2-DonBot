using DonBot.Core.Services.GuildWars2;
using DonBot.Tests.Infrastructure;

namespace DonBot.Tests.Services.GuildWars2;

public sealed class DiscordReportDeliveryClaimServiceTests
{
    [Fact]
    public async Task TryClaimAsync_SameGuildAndCanonicalUrlAllowsOnlyFirstSource()
    {
        using var db = new SqliteTestDb();
        var service = new DiscordReportDeliveryClaimService(db.Factory);

        var apiClaimed = await service.TryClaimAsync(
            42,
            "https://wvw.report/LD84-20260828-204103_wvw",
            DiscordReportDeliveryClaimService.ApiSource);
        var discordClaimed = await service.TryClaimAsync(
            42,
            "https://wvw.report/LD84-20260828-204103_wvw",
            DiscordReportDeliveryClaimService.DiscordSource);

        Assert.True(apiClaimed);
        Assert.False(discordClaimed);
    }

    [Fact]
    public async Task TryClaimAsync_DifferentGuildOrReportAllowsIndependentDelivery()
    {
        using var db = new SqliteTestDb();
        var service = new DiscordReportDeliveryClaimService(db.Factory);

        Assert.True(await service.TryClaimAsync(42, "https://wvw.report/first", "api"));
        Assert.True(await service.TryClaimAsync(43, "https://wvw.report/first", "api"));
        Assert.True(await service.TryClaimAsync(42, "https://wvw.report/second", "api"));
    }
}
