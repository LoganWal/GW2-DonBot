using DonBot.Core.Models.Enums;
using DonBot.Core.Models.GuildWars2;
using DonBot.Core.Services;

namespace DonBot.Tests.Services.GuildWarsServices;

public class FightLogProgressCalculatorTests
{
    [Fact]
    public void Calculate_HarvestTemple_IgnoresGreenMarkersAndUsesFinalDragon()
    {
        var data = new FightEliteInsightDataModel
        {
            Targets =
            [
                Target("The JormagVoid", 800, 0, 100),
                Target("The PrimordusVoid", 800, 0, 100),
                Target("The KralkatorrikVoid", 800, 0, 100),
                Target("The MordremothVoid", 800, 0, 100),
                Target("The ZhaitanVoid", 800, 0, 100),
                Target("The SooWonVoid", 800, 0, 200),
                Target("Soo-Won Green NE", 180, -1, -1)
            ]
        };

        var progress = FightLogProgressCalculator.Calculate(data, (short)FightTypesEnum.Ht, fightMode: 1);

        Assert.Equal(0m, progress.FightPercent);
        Assert.Equal(6, progress.FightPhase);
        Assert.Equal(0m, FightLogProgressCalculator.NormalizeForProgression((short)FightTypesEnum.Ht, 1, progress.FightPercent, progress.FightPhase));
    }

    [Fact]
    public void NormalizeForProgression_HarvestTemple_UsesPhasePosition()
    {
        var normalized = FightLogProgressCalculator.NormalizeForProgression((short)FightTypesEnum.Ht, 1, 50m, 3);

        Assert.Equal(58.33m, normalized);
    }

    [Fact]
    public void Calculate_UraChallenge_BeforeHealedPhaseMapsToPhaseOne()
    {
        var data = new FightEliteInsightDataModel
        {
            Targets = [Target("Godscream Ura", 1000, 10, 100)]
        };

        var progress = FightLogProgressCalculator.Calculate(data, (short)FightTypesEnum.Ura, fightMode: 2);

        Assert.Equal(10m, progress.FightPercent);
        Assert.Equal(1, progress.FightPhase);
        Assert.Equal(37m, FightLogProgressCalculator.NormalizeForProgression((short)FightTypesEnum.Ura, 2, progress.FightPercent, progress.FightPhase));
    }

    [Fact]
    public void Calculate_UraChallenge_AfterHealedPhaseMapsToPhaseTwo()
    {
        var data = new FightEliteInsightDataModel
        {
            Targets = [Target("Godscream Ura", 1000, 20, 100)],
            Phases = [new ArcDpsPhase { Name = "Healed", Targets = [0] }]
        };

        var progress = FightLogProgressCalculator.Calculate(data, (short)FightTypesEnum.Ura, fightMode: 1);

        Assert.Equal(20m, progress.FightPercent);
        Assert.Equal(2, progress.FightPhase);
        Assert.Equal(20m, FightLogProgressCalculator.NormalizeForProgression((short)FightTypesEnum.Ura, 1, progress.FightPercent, progress.FightPhase));
    }

    [Fact]
    public void Calculate_UraNormalMode_DoesNotSetPhase()
    {
        var data = new FightEliteInsightDataModel
        {
            Targets = [Target("Godscream Ura", 1000, 20, 100)],
            Phases = [new ArcDpsPhase { Name = "Healed", Targets = [0] }]
        };

        var progress = FightLogProgressCalculator.Calculate(data, (short)FightTypesEnum.Ura, fightMode: 0);

        Assert.Equal(20m, progress.FightPercent);
        Assert.Null(progress.FightPhase);
        Assert.Equal(20m, FightLogProgressCalculator.NormalizeForProgression((short)FightTypesEnum.Ura, 0, progress.FightPercent, progress.FightPhase));
    }

    [Fact]
    public void TryCalculateFromRaw_UsesRawFightModeForUra()
    {
        var raw = """
                  {
                    "fightMode": "Legendary Challenge Mode",
                    "targets": [
                      { "hbWidth": 1000, "hpLeft": 20, "health": 100, "name": "Godscream Ura" }
                    ],
                    "phases": [
                      { "name": "Full Fight", "targets": [0] },
                      { "name": "Healed", "targets": [0] }
                    ]
                  }
                  """;

        var ok = FightLogProgressCalculator.TryCalculateFromRaw(raw, (short)FightTypesEnum.Ura, fightMode: 0, out var progress);

        Assert.True(ok);
        Assert.Equal(20m, progress.FightPercent);
        Assert.Equal(2, progress.FightPhase);
    }

    private static ArcDpsTarget Target(string name, int hbWidth, long hpLeft, long health) => new()
    {
        Name = name,
        HbWidth = hbWidth,
        HpLeft = hpLeft,
        Health = health
    };
}
