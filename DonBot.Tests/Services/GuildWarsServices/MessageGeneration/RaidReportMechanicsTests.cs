using DonBot.Models.Entities;
using DonBot.Services.GuildWarsServices.MessageGeneration;

namespace DonBot.Tests.Services.GuildWarsServices.MessageGeneration;

public class RaidReportMechanicsTests
{
    // --- OrderedMechanicNames ---

    [Fact]
    public void OrderedMechanicNames_WhenEmpty_ReturnsEmpty()
    {
        var result = RaidReportService.OrderedMechanicNames([]);
        Assert.Empty(result);
    }

    [Fact]
    public void OrderedMechanicNames_WhenSingleMechanic_ReturnsThatName()
    {
        var mechanics = MechanicList((1L, "Black Oil Trigger", 3));

        var result = RaidReportService.OrderedMechanicNames(mechanics);

        Assert.Single(result);
        Assert.Equal("Black Oil Trigger", result[0]);
    }

    [Fact]
    public void OrderedMechanicNames_WhenMultipleMechanics_OrderedByAscendingTotal()
    {
        var mechanics = new List<PlayerFightLogMechanic>
        {
            new() { PlayerFightLogId = 1, MechanicName = "High", MechanicCount = 50 },
            new() { PlayerFightLogId = 1, MechanicName = "Low", MechanicCount = 5 },
            new() { PlayerFightLogId = 1, MechanicName = "Mid", MechanicCount = 20 },
        };

        var result = RaidReportService.OrderedMechanicNames(mechanics);

        Assert.Equal(["Low", "Mid", "High"], result);
    }

    [Fact]
    public void OrderedMechanicNames_WhenSameNameAcrossMultipleLogs_DeduplicatesAndSumsTotals()
    {
        var mechanics = new List<PlayerFightLogMechanic>
        {
            new() { PlayerFightLogId = 1, MechanicName = "A", MechanicCount = 10 },
            new() { PlayerFightLogId = 2, MechanicName = "A", MechanicCount = 10 },
            new() { PlayerFightLogId = 1, MechanicName = "B", MechanicCount = 5 },
        };

        var result = RaidReportService.OrderedMechanicNames(mechanics);

        Assert.Equal(2, result.Count);
        Assert.Equal("B", result[0]);
        Assert.Equal("A", result[1]);
    }

    // --- MechanicAccountTotals ---

    [Fact]
    public void MechanicAccountTotals_WhenEmpty_ReturnsEmpty()
    {
        var result = RaidReportService.MechanicAccountTotals("X", [], new Dictionary<long, string>());
        Assert.Empty(result);
    }

    [Fact]
    public void MechanicAccountTotals_WhenSingleEntry_ReturnsThatAccount()
    {
        var mechanics = new List<PlayerFightLogMechanic>
        {
            new() { PlayerFightLogId = 1, MechanicName = "Oils", MechanicCount = 3 }
        };
        var idToAccount = new Dictionary<long, string> { [1L] = "Alice.1234" };

        var result = RaidReportService.MechanicAccountTotals("Oils", mechanics, idToAccount);

        Assert.Single(result);
        Assert.Equal("Alice.1234", result[0].AccountName);
        Assert.Equal(3L, result[0].Total);
    }

    [Fact]
    public void MechanicAccountTotals_WhenMultipleLogsForSameAccount_SumsTotals()
    {
        var mechanics = new List<PlayerFightLogMechanic>
        {
            new() { PlayerFightLogId = 1, MechanicName = "Oils", MechanicCount = 2 },
            new() { PlayerFightLogId = 2, MechanicName = "Oils", MechanicCount = 3 }
        };
        var idToAccount = new Dictionary<long, string> { [1L] = "Alice.1234", [2L] = "Alice.1234" };

        var result = RaidReportService.MechanicAccountTotals("Oils", mechanics, idToAccount);

        Assert.Single(result);
        Assert.Equal(5L, result[0].Total);
    }

    [Fact]
    public void MechanicAccountTotals_WhenMultipleAccounts_OrderedByDescendingTotal()
    {
        var mechanics = new List<PlayerFightLogMechanic>
        {
            new() { PlayerFightLogId = 1, MechanicName = "Oils", MechanicCount = 1 },
            new() { PlayerFightLogId = 2, MechanicName = "Oils", MechanicCount = 5 },
            new() { PlayerFightLogId = 3, MechanicName = "Oils", MechanicCount = 3 }
        };
        var idToAccount = new Dictionary<long, string>
        {
            [1L] = "Alice.1234",
            [2L] = "Bob.5678",
            [3L] = "Carol.9999"
        };

        var result = RaidReportService.MechanicAccountTotals("Oils", mechanics, idToAccount);

        Assert.Equal(3, result.Count);
        Assert.Equal("Bob.5678", result[0].AccountName);
        Assert.Equal("Carol.9999", result[1].AccountName);
        Assert.Equal("Alice.1234", result[2].AccountName);
    }

    [Fact]
    public void MechanicAccountTotals_WhenPlayerFightLogIdNotInMap_IsExcluded()
    {
        var mechanics = new List<PlayerFightLogMechanic>
        {
            new() { PlayerFightLogId = 1, MechanicName = "Oils", MechanicCount = 4 },
            new() { PlayerFightLogId = 99, MechanicName = "Oils", MechanicCount = 10 }
        };
        var idToAccount = new Dictionary<long, string> { [1L] = "Alice.1234" };

        var result = RaidReportService.MechanicAccountTotals("Oils", mechanics, idToAccount);

        Assert.Single(result);
        Assert.Equal("Alice.1234", result[0].AccountName);
    }

    [Fact]
    public void MechanicAccountTotals_WhenDifferentMechanicName_IsExcluded()
    {
        var mechanics = new List<PlayerFightLogMechanic>
        {
            new() { PlayerFightLogId = 1, MechanicName = "Orbs", MechanicCount = 7 },
            new() { PlayerFightLogId = 1, MechanicName = "Oils", MechanicCount = 2 }
        };
        var idToAccount = new Dictionary<long, string> { [1L] = "Alice.1234" };

        var result = RaidReportService.MechanicAccountTotals("Oils", mechanics, idToAccount);

        Assert.Single(result);
        Assert.Equal(2L, result[0].Total);
    }

    [Fact]
    public void MechanicAccountTotals_WhenTotalIsZero_IsExcluded()
    {
        var mechanics = new List<PlayerFightLogMechanic>
        {
            new() { PlayerFightLogId = 1, MechanicName = "Oils", MechanicCount = 0 }
        };
        var idToAccount = new Dictionary<long, string> { [1L] = "Alice.1234" };

        var result = RaidReportService.MechanicAccountTotals("Oils", mechanics, idToAccount);

        Assert.Empty(result);
    }

    private static List<PlayerFightLogMechanic> MechanicList(params (long Id, string Name, long Count)[] entries) =>
        entries.Select(e => new PlayerFightLogMechanic
        {
            PlayerFightLogId = e.Id,
            MechanicName = e.Name,
            MechanicCount = e.Count
        }).ToList();
}
