using DonBot.Models.GuildWars2;
using DonBot.Services.GuildWarsServices;
using Newtonsoft.Json.Linq;

namespace DonBot.Tests.Services.GuildWarsServices;

public class PlayerServiceMechanicsTests
{
    private static readonly PlayerService Service = new(null!);

    [Fact]
    public void GetGw2Players_Mechanics_WhenSinglePlayerMechWithValue_PopulatesEntry()
    {
        var data = BuildData([new MechanicMap { Name = "Black Oil Trigger", PlayerMech = true }]);
        var phase = BuildPhaseWithMechanics([[5L]]);

        var players = Service.GetGw2Players(data, phase);

        Assert.Single(players);
        Assert.Equal(5L, players[0].Mechanics["Black Oil Trigger"]);
    }

    [Fact]
    public void GetGw2Players_Mechanics_WhenPlayerMechIsFalse_IsNotStored()
    {
        var data = BuildData([new MechanicMap { Name = "Environmental Effect", PlayerMech = false }]);
        var phase = BuildPhaseWithMechanics([[7L]]);

        var players = Service.GetGw2Players(data, phase);

        Assert.Single(players);
        Assert.Empty(players[0].Mechanics);
    }

    [Fact]
    public void GetGw2Players_Mechanics_WhenValueIsZero_IsNotStored()
    {
        var data = BuildData([new MechanicMap { Name = "Black Oil Trigger", PlayerMech = true }]);
        var phase = BuildPhaseWithMechanics([[0L]]);

        var players = Service.GetGw2Players(data, phase);

        Assert.Empty(players[0].Mechanics);
    }

    [Fact]
    public void GetGw2Players_Mechanics_WhenMechanicNameIsNull_IsSkipped()
    {
        var data = BuildData([new MechanicMap { Name = null, PlayerMech = true }]);
        var phase = BuildPhaseWithMechanics([[3L]]);

        var players = Service.GetGw2Players(data, phase);

        Assert.Empty(players[0].Mechanics);
    }

    [Fact]
    public void GetGw2Players_Mechanics_WhenMultipleMechanics_AllStoredCorrectly()
    {
        var data = BuildData([
            new MechanicMap { Name = "Insatiable Application", PlayerMech = true },
            new MechanicMap { Name = "Pool of Despair Hit", PlayerMech = true }
        ]);
        var phase = BuildPhaseWithMechanics([[4L], [2L]]);

        var players = Service.GetGw2Players(data, phase);

        Assert.Equal(4L, players[0].Mechanics["Insatiable Application"]);
        Assert.Equal(2L, players[0].Mechanics["Pool of Despair Hit"]);
    }

    [Fact]
    public void GetGw2Players_Mechanics_WhenMixedPlayerMech_OnlyPlayerMechStored()
    {
        var data = BuildData([
            new MechanicMap { Name = "Player Mech", PlayerMech = true },
            new MechanicMap { Name = "Env Mech", PlayerMech = false }
        ]);
        var phase = BuildPhaseWithMechanics([[3L], [9L]]);

        var players = Service.GetGw2Players(data, phase);

        Assert.Single(players[0].Mechanics);
        Assert.True(players[0].Mechanics.ContainsKey("Player Mech"));
        Assert.False(players[0].Mechanics.ContainsKey("Env Mech"));
    }

    [Fact]
    public void GetGw2Players_Mechanics_WhenNoMechanicMap_MechanicsIsEmpty()
    {
        var data = BuildData(null);
        var phase = BuildPhaseWithMechanics([]);

        var players = Service.GetGw2Players(data, phase);

        Assert.Empty(players[0].Mechanics);
    }

    [Fact]
    public void GetGw2Players_Mechanics_WhenMechanicStatsHasFewerEntriesThanMechanics_RemainingSkipped()
    {
        var data = BuildData([
            new MechanicMap { Name = "First", PlayerMech = true },
            new MechanicMap { Name = "Second", PlayerMech = true }
        ]);
        // Only one mechanic stat entry for two mechanics
        var phase = BuildPhaseWithMechanics([[6L]]);

        var players = Service.GetGw2Players(data, phase);

        Assert.Single(players[0].Mechanics);
        Assert.Equal(6L, players[0].Mechanics["First"]);
        Assert.False(players[0].Mechanics.ContainsKey("Second"));
    }

    private static EliteInsightDataModel BuildData(List<MechanicMap>? mechanicMap) =>
        new(new FightEliteInsightDataModel
        {
            Boons = [],
            MechanicMap = mechanicMap,
            Players = [new ArcDpsPlayer { Acc = "Test.1234", Profession = "Guardian", Name = "TestChar", NotInSquad = false, Group = 1 }]
        }, new HealingEliteInsightDataModel(), new BarrierEliteInsightDataModel(), null, null, null);

    // mechanicValues: one List<long> per mechanic slot (index matches possibleMechanics index)
    private static ArcDpsPhase BuildPhaseWithMechanics(List<List<long>> mechanicValues)
    {
        var playerMechanicStats = mechanicValues.Select(vals => (object)new JArray(vals)).ToList();
        return new ArcDpsPhase
        {
            MechanicStats = [playerMechanicStats],
            BuffsStatContainer = new BuffsStatContainer()
        };
    }
}
