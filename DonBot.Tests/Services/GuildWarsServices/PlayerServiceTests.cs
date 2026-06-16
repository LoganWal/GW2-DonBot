using DonBot.Core.Models.GuildWars2;
using DonBot.Models.Statics;
using DonBot.Services.GuildWarsServices;

namespace DonBot.Tests.Services.GuildWarsServices;

public class PlayerServiceTests
{
    private static readonly PlayerService Service = new(null!); // IEntityService not used by GetGw2Players

    [Fact]
    public void GetGw2Players_StabOnGroup_WhenStabilityAtStandardIndex_ReadsCorrectValue()
    {
        var boons = StandardBoons();
        var stabValue = 16.0;
        var data = BuildData(boons);
        var phase = BuildPhase(BoonData(boons.Count, boonIndex: boons.IndexOf(Gw2BoonIds.Stability), value: stabValue));

        var players = Service.GetGw2Players(data, phase);

        Assert.Single(players);
        Assert.Equal(stabValue, players[0].StabOnGroup);
    }

    [Fact]
    public void GetGw2Players_StabOnGroup_WhenStabilityAtNonStandardIndex_ReadsFromCorrectIndex()
    {
        var boons = new List<int> { Gw2BoonIds.Stability, 740, 725, Gw2BoonIds.Quickness, Gw2BoonIds.Alacrity, 717, 718, 726, 743, 719, 26980, 873 };
        var stabValue = 16.0;
        var data = BuildData(boons);
        var phase = BuildPhase(BoonData(boons.Count, boonIndex: 0, value: stabValue));

        var players = Service.GetGw2Players(data, phase);

        Assert.Single(players);
        Assert.Equal(stabValue, players[0].StabOnGroup);
    }

    [Fact]
    public void GetGw2Players_StabOnGroup_WhenStabilityNotInBoonsList_ReturnsZero()
    {
        var boons = new List<int> { 740, 725, Gw2BoonIds.Quickness, Gw2BoonIds.Alacrity };
        var data = BuildData(boons);
        var phase = BuildPhase(BoonData(boons.Count, boonIndex: -1, value: 16.0));

        var players = Service.GetGw2Players(data, phase);

        Assert.Single(players);
        Assert.Equal(0.0, players[0].StabOnGroup);
    }

    [Fact]
    public void GetGw2Players_TotalQuick_WhenQuicknessAtNonStandardIndex_ReadsFromCorrectIndex()
    {
        var boons = new List<int> { Gw2BoonIds.Quickness, 740, 725, Gw2BoonIds.Alacrity, 717, 718, 726, 743, Gw2BoonIds.Stability, 719, 26980, 873 };
        var quickValue = 0.9;
        var data = BuildData(boons);
        var phase = BuildPhase(BoonData(boons.Count, boonIndex: 8, value: 0), activeData: ActiveBoonData(boons.Count, quickIndex: 0, quickValue: quickValue));

        var players = Service.GetGw2Players(data, phase);

        Assert.Single(players);
        Assert.Equal(quickValue, players[0].TotalQuick);
    }

    [Fact]
    public void GetGw2Players_TotalAlac_WhenAlacAtNonStandardIndex_ReadsFromCorrectIndex()
    {
        var boons = new List<int> { Gw2BoonIds.Alacrity, 740, 725, Gw2BoonIds.Quickness, 717, 718, 726, 743, Gw2BoonIds.Stability, 719, 26980, 873 };
        var alacValue = 0.75;
        var data = BuildData(boons);
        var phase = BuildPhase(BoonData(boons.Count, boonIndex: 8, value: 0), activeData: ActiveBoonData(boons.Count, alacIndex: 0, alacValue: alacValue));

        var players = Service.GetGw2Players(data, phase);

        Assert.Single(players);
        Assert.Equal(alacValue, players[0].TotalAlac);
    }

    [Fact]
    public void GetGw2Players_QuicknessGenGroup_WhenQuicknessAtNonStandardIndex_ReadsFromCorrectIndex()
    {
        var boons = new List<int> { 740, 725, Gw2BoonIds.Alacrity, Gw2BoonIds.Quickness, 717, 718, 726, 743, Gw2BoonIds.Stability, 719, 26980, 873 };
        var quickIndex = boons.IndexOf(Gw2BoonIds.Quickness);
        var quickGenValue = 72.5;
        var activeQuickValue = 12.25;
        var data = BuildData(boons);
        var phase = BuildPhase(
            BoonData(boons.Count, boonIndex: quickIndex, value: quickGenValue),
            activeData: ActiveBoonData(boons.Count, quickIndex: quickIndex, quickValue: activeQuickValue));

        var players = Service.GetGw2Players(data, phase);

        Assert.Single(players);
        Assert.Equal(quickGenValue, players[0].QuicknessGenGroup);
        Assert.Equal(activeQuickValue, players[0].TotalQuick);
    }

    [Fact]
    public void GetGw2Players_AlacGenGroup_WhenAlacAtNonStandardIndex_ReadsFromCorrectIndex()
    {
        var boons = new List<int> { 740, 725, Gw2BoonIds.Quickness, 717, Gw2BoonIds.Alacrity, 718, 726, 743, Gw2BoonIds.Stability, 719, 26980, 873 };
        var alacIndex = boons.IndexOf(Gw2BoonIds.Alacrity);
        var alacGenValue = 81.25;
        var activeAlacValue = 9.5;
        var data = BuildData(boons);
        var phase = BuildPhase(
            BoonData(boons.Count, boonIndex: alacIndex, value: alacGenValue),
            activeData: ActiveBoonData(boons.Count, alacIndex: alacIndex, alacValue: activeAlacValue));

        var players = Service.GetGw2Players(data, phase);

        Assert.Single(players);
        Assert.Equal(alacGenValue, players[0].AlacGenGroup);
        Assert.Equal(activeAlacValue, players[0].TotalAlac);
    }

    [Fact]
    public void GetGw2Players_TimeOfDeath_WhenAccountHasMultipleCharactersAndOnlySecondDied_RecordsDeathTime()
    {
        // Regression: death data can appear on a later character for the same account.
        var boons = StandardBoons();
        var data = new EliteInsightDataModel(
            new FightEliteInsightDataModel
            {
                Boons = boons,
                Players =
                [
                    new ArcDpsPlayer { Acc = "Same.1234", Profession = "Guardian", Name = "CharA", NotInSquad = false, Group = 1, Details = new ArcsDpsPlayerDetails { DeathRecap = [] } },
                    new ArcDpsPlayer { Acc = "Same.1234", Profession = "Warrior",  Name = "CharB", NotInSquad = false, Group = 1, Details = new ArcsDpsPlayerDetails { DeathRecap = [new DeathRecap { Time = 1000 }] } }
                ]
            },
            new HealingEliteInsightDataModel(),
            new BarrierEliteInsightDataModel(),
            null, null, null);
        var phase = BuildPhase(BoonData(boons.Count, boonIndex: boons.IndexOf(Gw2BoonIds.Stability), value: 0));

        var players = Service.GetGw2Players(data, phase);

        Assert.Single(players);
        Assert.Equal(1000L, players[0].TimeOfDeath);
    }

    [Fact]
    public void GetGw2Players_TimeOfDeath_WhenAccountHasMultipleCharactersWithDeaths_KeepsEarliest()
    {
        var boons = StandardBoons();
        var data = new EliteInsightDataModel(
            new FightEliteInsightDataModel
            {
                Boons = boons,
                Players =
                [
                    new ArcDpsPlayer { Acc = "Same.1234", Profession = "Guardian", Name = "CharA", NotInSquad = false, Group = 1, Details = new ArcsDpsPlayerDetails { DeathRecap = [new DeathRecap { Time = 5000 }] } },
                    new ArcDpsPlayer { Acc = "Same.1234", Profession = "Warrior",  Name = "CharB", NotInSquad = false, Group = 1, Details = new ArcsDpsPlayerDetails { DeathRecap = [new DeathRecap { Time = 2000 }] } }
                ]
            },
            new HealingEliteInsightDataModel(),
            new BarrierEliteInsightDataModel(),
            null, null, null);
        var phase = BuildPhase(BoonData(boons.Count, boonIndex: boons.IndexOf(Gw2BoonIds.Stability), value: 0));

        var players = Service.GetGw2Players(data, phase);

        Assert.Single(players);
        Assert.Equal(2000L, players[0].TimeOfDeath);
    }

    [Fact]
    public void GetGw2Players_TotalQuick_WhenQuicknessNotInBoonsList_ReturnsZero()
    {
        var boons = new List<int> { 740, 725, Gw2BoonIds.Alacrity };
        var data = BuildData(boons);
        var phase = BuildPhase(BoonData(boons.Count, boonIndex: -1, value: 0));

        var players = Service.GetGw2Players(data, phase);

        Assert.Single(players);
        Assert.Equal(0.0, players[0].TotalQuick);
    }

    private static List<int> StandardBoons() =>
        [740, 725, Gw2BoonIds.Quickness, Gw2BoonIds.Alacrity, 717, 718, 726, 743, Gw2BoonIds.Stability, 719, 26980, 873];

    private static EliteInsightDataModel BuildData(List<int> boons) =>
        new(new FightEliteInsightDataModel
        {
            Boons = boons,
            Players = [new ArcDpsPlayer { Acc = "Test.1234", Profession = "Guardian", Name = "TestChar", NotInSquad = false, Group = 1 }]
        }, new HealingEliteInsightDataModel(), new BarrierEliteInsightDataModel(), null, null, null);

    private static List<List<double>> BoonData(int boonCount, int boonIndex, double value) =>
        Enumerable.Range(0, boonCount)
            .Select(i => new List<double> { i == boonIndex ? value : 0.0 })
            .ToList();

    private static List<List<double>> ActiveBoonData(int boonCount, int quickIndex = -1, double quickValue = 0, int alacIndex = -1, double alacValue = 0) =>
        Enumerable.Range(0, boonCount)
            .Select(i => new List<double> { i == quickIndex ? quickValue : i == alacIndex ? alacValue : 0.0 })
            .ToList();

    private static ArcDpsPhase BuildPhase(List<List<double>> genData, List<List<double>>? activeData = null)
    {
        activeData ??= genData;
        return new ArcDpsPhase
        {
            BuffsStatContainer = new BuffsStatContainer
            {
                BoonGenGroupStats = [new BoonActiveStat { Data = genData }],
                BoonGenOGroupStats = [new BoonActiveStat { Data = genData }],
                BoonActiveStats = [new BoonActiveStat { Data = activeData }]
            }
        };
    }
}
