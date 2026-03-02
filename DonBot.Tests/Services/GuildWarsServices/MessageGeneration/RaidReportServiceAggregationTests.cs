using DonBot.Models.Entities;
using DonBot.Services.GuildWarsServices.MessageGeneration;

namespace DonBot.Tests.Services.GuildWarsServices.MessageGeneration;

public class RaidReportServiceAggregationTests
{
    private const string Name = "Alice.1234";

    // --- Damage ---

    [Fact]
    public void AggregatePlayerFights_Damage_WhenAllFightsHaveValue_AveragesCorrectly()
    {
        var result = Aggregate(
            new() { GuildWarsAccountName = Name, Damage = 1000 },
            new() { GuildWarsAccountName = Name, Damage = 2000 });

        Assert.Equal(1500, result.Damage);
    }

    [Fact]
    public void AggregatePlayerFights_Damage_WhenSomeFightsAreZero_IncludesZerosInAverage()
    {
        var result = Aggregate(
            new() { GuildWarsAccountName = Name, Damage = 1000 },
            new() { GuildWarsAccountName = Name, Damage = 0 });

        Assert.Equal(500, result.Damage);
    }

    [Fact]
    public void AggregatePlayerFights_Damage_WhenAllFightsAreZero_ReturnsZero()
    {
        var result = Aggregate(
            new() { GuildWarsAccountName = Name, Damage = 0 },
            new() { GuildWarsAccountName = Name, Damage = 0 });

        Assert.Equal(0, result.Damage);
    }

    // --- StabOnGroup ---

    [Fact]
    public void AggregatePlayerFights_StabOnGroup_WhenAllFightsHaveValue_AveragesCorrectly()
    {
        var result = Aggregate(
            new() { GuildWarsAccountName = Name, StabGenOnGroup = 16 },
            new() { GuildWarsAccountName = Name, StabGenOnGroup = 12 });

        Assert.Equal(14.0, result.StabOnGroup);
    }

    [Fact]
    public void AggregatePlayerFights_StabOnGroup_WhenSomeFightsAreZero_IncludesZerosInAverage()
    {
        // Core scenario: one great stab fight, then player switches class → average must not be inflated
        var result = Aggregate(
            new() { GuildWarsAccountName = Name, StabGenOnGroup = 16 },
            new() { GuildWarsAccountName = Name, StabGenOnGroup = 0 });

        Assert.Equal(8.0, result.StabOnGroup);
    }

    [Fact]
    public void AggregatePlayerFights_StabOnGroup_WhenAllFightsAreZero_ReturnsZero()
    {
        var result = Aggregate(
            new() { GuildWarsAccountName = Name, StabGenOnGroup = 0 },
            new() { GuildWarsAccountName = Name, StabGenOnGroup = 0 });

        Assert.Equal(0.0, result.StabOnGroup);
    }

    [Fact]
    public void AggregatePlayerFights_StabOnGroup_OneGoodFightAmongMany_DilutesOutlier()
    {
        // 1 fight at 20, 19 fights at 0 → average = 1.0 (not 20)
        var logs = new List<PlayerFightLog>
        {
            new() { GuildWarsAccountName = Name, StabGenOnGroup = 20 }
        };
        logs.AddRange(Enumerable.Range(0, 19).Select(_ => new PlayerFightLog { GuildWarsAccountName = Name, StabGenOnGroup = 0 }));

        var result = RaidReportService.AggregatePlayerFights(logs.GroupBy(l => l.GuildWarsAccountName).First());

        Assert.Equal(1.0, result.StabOnGroup);
    }

    // --- Cleanses ---

    [Fact]
    public void AggregatePlayerFights_Cleanses_WhenSomeFightsAreZero_IncludesZerosInAverage()
    {
        var result = Aggregate(
            new() { GuildWarsAccountName = Name, Cleanses = 100 },
            new() { GuildWarsAccountName = Name, Cleanses = 0 });

        Assert.Equal(50.0, result.Cleanses);
    }

    // --- Healing ---

    [Fact]
    public void AggregatePlayerFights_Healing_WhenSomeFightsAreZero_IncludesZerosInAverage()
    {
        var result = Aggregate(
            new() { GuildWarsAccountName = Name, Healing = 50000 },
            new() { GuildWarsAccountName = Name, Healing = 0 });

        Assert.Equal(25000, result.Healing);
    }

    // --- TotalQuick / TotalAlac ---

    [Fact]
    public void AggregatePlayerFights_TotalQuick_WhenSomeFightsAreZero_IncludesZerosInAverage()
    {
        var result = Aggregate(
            new() { GuildWarsAccountName = Name, QuicknessDuration = 0.8m },
            new() { GuildWarsAccountName = Name, QuicknessDuration = 0m });

        Assert.Equal(0.4, result.TotalQuick);
    }

    [Fact]
    public void AggregatePlayerFights_TotalAlac_WhenSomeFightsAreZero_IncludesZerosInAverage()
    {
        var result = Aggregate(
            new() { GuildWarsAccountName = Name, AlacDuration = 1.0m },
            new() { GuildWarsAccountName = Name, AlacDuration = 0m });

        Assert.Equal(0.5, result.TotalAlac);
    }

    // --- AccountName / SubGroup ---

    [Fact]
    public void AggregatePlayerFights_AccountName_IncludesFightCount()
    {
        var result = Aggregate(
            new() { GuildWarsAccountName = Name },
            new() { GuildWarsAccountName = Name },
            new() { GuildWarsAccountName = Name });

        Assert.StartsWith("(3)", result.AccountName);
        Assert.Contains(Name, result.AccountName);
    }

    [Fact]
    public void AggregatePlayerFights_SubGroup_TakesMostCommonSubGroup()
    {
        var result = Aggregate(
            new() { GuildWarsAccountName = Name, SubGroup = 1 },
            new() { GuildWarsAccountName = Name, SubGroup = 2 },
            new() { GuildWarsAccountName = Name, SubGroup = 2 });

        Assert.Equal(2, result.SubGroup);
    }

    // --- Sums (verify unchanged behaviour) ---

    [Fact]
    public void AggregatePlayerFights_Deaths_SumsAcrossAllFights()
    {
        var result = Aggregate(
            new() { GuildWarsAccountName = Name, Deaths = 1 },
            new() { GuildWarsAccountName = Name, Deaths = 2 });

        Assert.Equal(3, result.Deaths);
    }

    [Fact]
    public void AggregatePlayerFights_DamageTaken_SumsAcrossAllFights()
    {
        var result = Aggregate(
            new() { GuildWarsAccountName = Name, DamageTaken = 100_000 },
            new() { GuildWarsAccountName = Name, DamageTaken = 200_000 });

        Assert.Equal(300_000, result.DamageTaken);
    }

    // --- Helper ---

    private static DonBot.Models.GuildWars2.Gw2Player Aggregate(params PlayerFightLog[] logs) =>
        RaidReportService.AggregatePlayerFights(logs.GroupBy(l => l.GuildWarsAccountName).First());
}
