using DonBot.Core.Models.Entities;
using DonBot.Core.Models.GuildWars2;
using DonBot.Core.Services;

namespace DonBot.Tests.Services.GuildWarsServices;

public class PlayerFightLogPlaystyleClassifierTests
{
    [Fact]
    public void ResolvePvePlaystyle_WhenBoonRoleIsEmpty_ReturnsDps()
    {
        var playstyle = PlayerFightLogPlaystyleClassifier.ResolvePvePlaystyle(string.Empty);

        Assert.Equal(PlayerFightLogPlaystyleClassifier.DpsPlaystyle, playstyle);
    }

    [Fact]
    public void ResolvePvePlaystyle_WhenBoonRoleIsBoonDps_ReturnsBoonDps()
    {
        var playstyle = PlayerFightLogPlaystyleClassifier.ResolvePvePlaystyle(PlayerFightLogRoleClassifier.BoonDpsRole);

        Assert.Equal(PlayerFightLogPlaystyleClassifier.BoonDpsPlaystyle, playstyle);
    }

    [Fact]
    public void ResolvePvePlaystyle_WhenDpsIsLowAndBoonGenerationIsHigh_ReturnsBoonHealer()
    {
        var player = new Gw2Player
        {
            Damage = 120_000,
            QuicknessGenGroup = 80
        };

        var playstyle = PlayerFightLogPlaystyleClassifier.ResolvePvePlaystyle(player, 60_000, averageGroupDps: 10_000);

        Assert.Equal(PlayerFightLogPlaystyleClassifier.BoonHealerPlaystyle, playstyle);
    }

    [Fact]
    public void ResolvePvePlaystyle_WhenDpsIsLowAndBoonGenerationIsMissing_ReturnsMechanic()
    {
        var player = new Gw2Player
        {
            Damage = 120_000
        };

        var playstyle = PlayerFightLogPlaystyleClassifier.ResolvePvePlaystyle(player, 60_000, averageGroupDps: 10_000);

        Assert.Equal(PlayerFightLogPlaystyleClassifier.MechanicPlaystyle, playstyle);
    }

    [Fact]
    public void ResolveWvwPlaystyle_WhenDamageIsHighAndSupportIsLow_ReturnsDps()
    {
        var playstyle = PlayerFightLogPlaystyleClassifier.ResolveWvwPlaystyle(
            Player(60_000),
            60_000,
            Benchmarks());

        Assert.Equal(PlayerFightLogPlaystyleClassifier.DpsPlaystyle, playstyle);
    }

    [Fact]
    public void ResolveWvwPlaystyle_WhenDamageAndSupportAreDecent_ReturnsSupportDps()
    {
        var playstyle = PlayerFightLogPlaystyleClassifier.ResolveWvwPlaystyle(
            Player(30_000, cleanses: 6),
            60_000,
            Benchmarks());

        Assert.Equal(PlayerFightLogPlaystyleClassifier.WvwSupportDpsPlaystyle, playstyle);
    }

    [Fact]
    public void ResolveWvwPlaystyle_WhenDamageIsLimitedAndSupportIsDecent_ReturnsSupport()
    {
        var playstyle = PlayerFightLogPlaystyleClassifier.ResolveWvwPlaystyle(
            Player(6_000, strips: 3),
            60_000,
            Benchmarks());

        Assert.Equal(PlayerFightLogPlaystyleClassifier.WvwSupportPlaystyle, playstyle);
    }

    [Fact]
    public void ResolveWvwPlaystyle_WhenDamageIsLimitedAndStabilityMeetsTinyBenchmark_ReturnsSupport()
    {
        var playstyle = PlayerFightLogPlaystyleClassifier.ResolveWvwPlaystyle(
            Player(6_000, stab: 0.25m),
            60_000,
            Benchmarks(stability: 0.25));

        Assert.Equal(PlayerFightLogPlaystyleClassifier.WvwSupportPlaystyle, playstyle);
    }

    [Fact]
    public void ResolveWvwPlaystyle_WhenStabilityBenchmarkIsMissing_DoesNotTreatStabilityAsSupport()
    {
        var playstyle = PlayerFightLogPlaystyleClassifier.ResolveWvwPlaystyle(
            Player(6_000, stab: 0.25m),
            60_000,
            Benchmarks(stability: 0));

        Assert.Equal(PlayerFightLogPlaystyleClassifier.DpsPlaystyle, playstyle);
    }

    [Fact]
    public void ResolveWvwPlaystyle_WhenDamageIsLimitedAndHealingIsGood_ReturnsHealSupport()
    {
        var playstyle = PlayerFightLogPlaystyleClassifier.ResolveWvwPlaystyle(
            Player(6_000, healing: 60_000),
            60_000,
            Benchmarks());

        Assert.Equal(PlayerFightLogPlaystyleClassifier.WvwHealSupportPlaystyle, playstyle);
    }

    private static WvwPlaystyleBenchmarks Benchmarks(double stability = 5) => new(
        HighDamagePerSecond: 750,
        DecentDamagePerSecond: 300,
        DecentHealingPerSecond: 250,
        GoodHealingPerSecond: 500,
        DecentCleansesPerMinute: 3,
        DecentStripsPerMinute: 1,
        DecentStabilityGeneration: stability);

    private static PlayerFightLog Player(
        long damage,
        long healing = 0,
        long cleanses = 0,
        long strips = 0,
        decimal stab = 0) => new()
    {
        Damage = damage,
        Healing = healing,
        Cleanses = cleanses,
        Strips = strips,
        StabGenOnGroup = stab
    };
}
