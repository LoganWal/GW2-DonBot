using DonBot.Models.GuildWars2;
using DonBot.Models.Statics;
using DonBot.Services.GuildWarsServices;

namespace DonBot.Tests.Services.GuildWarsServices;

public class PlayerServiceTests
{
    // GetGw2Players doesn't use IEntityService so null is safe here
    private static readonly PlayerService Service = new(null!);

    // --- Stability boon index lookup ---

    [Fact]
    public void GetGw2Players_StabOnGroup_WhenStabilityAtStandardIndex_ReadsCorrectValue()
    {
        // Standard API order: Stability is at index 8
        var boons = StandardBoons();
        var stabValue = 16.0;
        var data = BuildData(boons);
        var phase = BuildPhase(BoonData(boons.Count, stabIndex: boons.IndexOf(Gw2BoonIds.Stability), stabValue: stabValue));

        var players = Service.GetGw2Players(data, phase);

        Assert.Single(players);
        Assert.Equal(stabValue, players[0].StabOnGroup);
    }

    [Fact]
    public void GetGw2Players_StabOnGroup_WhenStabilityAtNonStandardIndex_ReadsFromCorrectIndex()
    {
        // Swap Stability to index 0 to prove dynamic lookup, not hardcoded index
        var boons = new List<int> { Gw2BoonIds.Stability, 740, 725, Gw2BoonIds.Quickness, Gw2BoonIds.Alacrity, 717, 718, 726, 743, 719, 26980, 873 };
        var stabValue = 16.0;
        var data = BuildData(boons);
        var phase = BuildPhase(BoonData(boons.Count, stabIndex: 0, stabValue: stabValue));

        var players = Service.GetGw2Players(data, phase);

        Assert.Single(players);
        Assert.Equal(stabValue, players[0].StabOnGroup);
    }

    [Fact]
    public void GetGw2Players_StabOnGroup_WhenStabilityNotInBoonsList_ReturnsZero()
    {
        // Boons list has no Stability ID
        var boons = new List<int> { 740, 725, Gw2BoonIds.Quickness, Gw2BoonIds.Alacrity };
        var data = BuildData(boons);
        var phase = BuildPhase(BoonData(boons.Count, stabIndex: -1, stabValue: 16.0));

        var players = Service.GetGw2Players(data, phase);

        Assert.Single(players);
        Assert.Equal(0.0, players[0].StabOnGroup);
    }

    // --- Quickness boon index lookup ---

    [Fact]
    public void GetGw2Players_TotalQuick_WhenQuicknessAtNonStandardIndex_ReadsFromCorrectIndex()
    {
        // Move Quickness to index 0
        var boons = new List<int> { Gw2BoonIds.Quickness, 740, 725, Gw2BoonIds.Alacrity, 717, 718, 726, 743, Gw2BoonIds.Stability, 719, 26980, 873 };
        var quickValue = 0.9;
        var data = BuildData(boons);
        var phase = BuildPhase(BoonData(boons.Count, stabIndex: 8, stabValue: 0), activeData: ActiveBoonData(boons.Count, quickIndex: 0, quickValue: quickValue));

        var players = Service.GetGw2Players(data, phase);

        Assert.Single(players);
        Assert.Equal(quickValue, players[0].TotalQuick);
    }

    [Fact]
    public void GetGw2Players_TotalAlac_WhenAlacAtNonStandardIndex_ReadsFromCorrectIndex()
    {
        // Move Alacrity to index 0
        var boons = new List<int> { Gw2BoonIds.Alacrity, 740, 725, Gw2BoonIds.Quickness, 717, 718, 726, 743, Gw2BoonIds.Stability, 719, 26980, 873 };
        var alacValue = 0.75;
        var data = BuildData(boons);
        var phase = BuildPhase(BoonData(boons.Count, stabIndex: 8, stabValue: 0), activeData: ActiveBoonData(boons.Count, alacIndex: 0, alacValue: alacValue));

        var players = Service.GetGw2Players(data, phase);

        Assert.Single(players);
        Assert.Equal(alacValue, players[0].TotalAlac);
    }

    [Fact]
    public void GetGw2Players_TotalQuick_WhenQuicknessNotInBoonsList_ReturnsZero()
    {
        var boons = new List<int> { 740, 725, Gw2BoonIds.Alacrity };
        var data = BuildData(boons);
        var phase = BuildPhase(BoonData(boons.Count, stabIndex: -1, stabValue: 0));

        var players = Service.GetGw2Players(data, phase);

        Assert.Single(players);
        Assert.Equal(0.0, players[0].TotalQuick);
    }

    // --- Helpers ---

    private static List<int> StandardBoons() =>
        [740, 725, Gw2BoonIds.Quickness, Gw2BoonIds.Alacrity, 717, 718, 726, 743, Gw2BoonIds.Stability, 719, 26980, 873];

    private static EliteInsightDataModel BuildData(List<int> boons) =>
        new(new FightEliteInsightDataModel
        {
            Boons = boons,
            Players = [new ArcDpsPlayer { Acc = "Test.1234", Profession = "Guardian", Name = "TestChar", NotInSquad = false, Group = 1 }]
        }, new HealingEliteInsightDataModel(), new BarrierEliteInsightDataModel());

    /// <summary>
    /// Builds boon gen group/off-group data (for stab). All values 0 except the given stab index.
    /// </summary>
    private static List<List<double>> BoonData(int boonCount, int stabIndex, double stabValue) =>
        Enumerable.Range(0, boonCount)
            .Select(i => new List<double> { i == stabIndex ? stabValue : 0.0 })
            .ToList();

    /// <summary>
    /// Builds boon active stats data (for quick/alac). All values 0 except the given indices.
    /// </summary>
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
