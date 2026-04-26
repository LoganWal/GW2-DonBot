using DonBot.Models.Entities;
using DonBot.Services.GuildWarsServices.MessageGeneration;

namespace DonBot.Tests.Services.GuildWarsServices.MessageGeneration;

public class RaidReportMechanicsDiscordFormatTests
{
    // --- BuildMechanicRows ---

    [Fact]
    public void BuildMechanicRows_WhenNoMechanics_ReturnsEmptyList()
    {
        var result = RaidReportService.BuildMechanicRows([], new Dictionary<long, string>());
        Assert.Empty(result);
    }

    [Fact]
    public void BuildMechanicRows_SingleMechanic_SingleAccount_IncludesMechanicName()
    {
        var mechanics = Mechanics((1L, "Black Oil", 3L));
        var idToAccount = new Dictionary<long, string> { [1L] = "Alice.1234" };

        var rows = RaidReportService.BuildMechanicRows(mechanics, idToAccount);

        Assert.Single(rows);
        Assert.Contains("Black Oil", rows[0]);
    }

    [Fact]
    public void BuildMechanicRows_SingleMechanic_SingleAccount_IncludesTotalCount()
    {
        var mechanics = Mechanics((1L, "Oils", 7L));
        var idToAccount = new Dictionary<long, string> { [1L] = "Alice.1234" };

        var rows = RaidReportService.BuildMechanicRows(mechanics, idToAccount);

        Assert.Contains("7", rows[0]);
    }

    [Fact]
    public void BuildMechanicRows_SingleMechanic_SingleAccount_IncludesAccountName()
    {
        var mechanics = Mechanics((1L, "Oils", 3L));
        var idToAccount = new Dictionary<long, string> { [1L] = "Alice.1234" };

        var rows = RaidReportService.BuildMechanicRows(mechanics, idToAccount);

        Assert.Contains("Alice.1234", rows[0]);
    }

    [Fact]
    public void BuildMechanicRows_SingleMechanic_SingleAccount_IncludesAccountCountInParens()
    {
        var mechanics = Mechanics((1L, "Oils", 5L));
        var idToAccount = new Dictionary<long, string> { [1L] = "Alice.1234" };

        var rows = RaidReportService.BuildMechanicRows(mechanics, idToAccount);

        Assert.Contains("(5)", rows[0]);
    }

    [Fact]
    public void BuildMechanicRows_EachRowEndsWithNewline()
    {
        var mechanics = Mechanics((1L, "Oils", 2L), (2L, "Trigger", 4L));
        var idToAccount = new Dictionary<long, string> { [1L] = "Alice.1234", [2L] = "Bob.5678" };

        var rows = RaidReportService.BuildMechanicRows(mechanics, idToAccount);

        Assert.All(rows, r => Assert.EndsWith("\n", r));
    }

    [Fact]
    public void BuildMechanicRows_MultipleAccounts_ShowsAccountWithHighestTotal()
    {
        var mechanics = new List<PlayerFightLogMechanic>
        {
            new() { PlayerFightLogId = 1, MechanicName = "Oils", MechanicCount = 2 },
            new() { PlayerFightLogId = 2, MechanicName = "Oils", MechanicCount = 8 },
        };
        var idToAccount = new Dictionary<long, string>
        {
            [1L] = "LowScorer.1234",
            [2L] = "TopScorer.5678",
        };

        var rows = RaidReportService.BuildMechanicRows(mechanics, idToAccount);

        Assert.Single(rows);
        // ClipAt(13): "TopScorer.567" (13 chars); "LowScorer.123" (13 chars)
        Assert.Contains("TopScorer.567", rows[0]);
        Assert.DoesNotContain("LowScorer", rows[0]);
    }

    [Fact]
    public void BuildMechanicRows_MultipleAccounts_TotalIsSum()
    {
        var mechanics = new List<PlayerFightLogMechanic>
        {
            new() { PlayerFightLogId = 1, MechanicName = "Oils", MechanicCount = 3 },
            new() { PlayerFightLogId = 2, MechanicName = "Oils", MechanicCount = 5 },
        };
        var idToAccount = new Dictionary<long, string>
        {
            [1L] = "Alice.1234",
            [2L] = "Bob.5678",
        };

        var rows = RaidReportService.BuildMechanicRows(mechanics, idToAccount);

        Assert.Contains("8", rows[0]);
    }

    [Fact]
    public void BuildMechanicRows_MultipleMechanics_OrderedByAscendingTotal()
    {
        var mechanics = new List<PlayerFightLogMechanic>
        {
            new() { PlayerFightLogId = 1, MechanicName = "Heavy", MechanicCount = 10 },
            new() { PlayerFightLogId = 1, MechanicName = "Light", MechanicCount = 2 },
        };
        var idToAccount = new Dictionary<long, string> { [1L] = "Alice.1234" };

        var rows = RaidReportService.BuildMechanicRows(mechanics, idToAccount);

        Assert.Equal(2, rows.Count);
        Assert.Contains("Light", rows[0]);
        Assert.Contains("Heavy", rows[1]);
    }

    [Fact]
    public void BuildMechanicRows_LongMechanicName_IsClippedAt18Characters()
    {
        const string longName = "SuperLongMechanicNameHere";
        var mechanics = Mechanics((1L, longName, 1L));
        var idToAccount = new Dictionary<long, string> { [1L] = "Alice.1234" };

        var rows = RaidReportService.BuildMechanicRows(mechanics, idToAccount);

        var row = rows[0];
        Assert.StartsWith("SuperLongMechanicN", row);
        Assert.DoesNotContain("SuperLongMechanicNa", row.Split("  ")[0]);
    }

    [Fact]
    public void BuildMechanicRows_LongAccountName_IsClippedAt13Characters()
    {
        var mechanics = Mechanics((1L, "Oils", 5L));
        var idToAccount = new Dictionary<long, string> { [1L] = "VeryLongAccountName.9999" };

        var rows = RaidReportService.BuildMechanicRows(mechanics, idToAccount);

        // ClipAt(13) = "VeryLongAccou" (13 chars: V-e-r-y-L-o-n-g-A-c-c-o-u)
        Assert.Contains("VeryLongAccou (5)", rows[0]);
        Assert.DoesNotContain("VeryLongAccoun", rows[0]);
    }

    [Fact]
    public void BuildMechanicRows_AccountNotInMap_ShowsDash()
    {
        var mechanics = Mechanics((99L, "Oils", 5L));
        var idToAccount = new Dictionary<long, string>();

        var rows = RaidReportService.BuildMechanicRows(mechanics, idToAccount);

        Assert.Single(rows);
        Assert.Contains("-", rows[0]);
    }

    [Fact]
    public void BuildMechanicRows_AllZeroCounts_AllAccountsExcluded_ShowsDash()
    {
        var mechanics = Mechanics((1L, "Oils", 0L));
        var idToAccount = new Dictionary<long, string> { [1L] = "Alice.1234" };

        var rows = RaidReportService.BuildMechanicRows(mechanics, idToAccount);

        Assert.Single(rows);
        Assert.Contains("-", rows[0]);
        Assert.DoesNotContain("Alice.1234", rows[0]);
    }

    [Fact]
    public void BuildMechanicRows_MechanicNamePaddedTo18_ColumnsAlignAcrossRows()
    {
        var mechanics = new List<PlayerFightLogMechanic>
        {
            new() { PlayerFightLogId = 1, MechanicName = "Short", MechanicCount = 3 },
            new() { PlayerFightLogId = 1, MechanicName = "MediumLengthName", MechanicCount = 7 },
        };
        var idToAccount = new Dictionary<long, string> { [1L] = "Alice.1234" };

        var rows = RaidReportService.BuildMechanicRows(mechanics, idToAccount);

        // The total count field starts at the same column in each row (after 20 chars: 18 name + 2 spaces)
        Assert.Equal(rows[0].IndexOf('3'), rows[1].IndexOf('7'));
    }

    private static List<PlayerFightLogMechanic> Mechanics(params (long Id, string Name, long Count)[] entries) =>
        entries.Select(e => new PlayerFightLogMechanic
        {
            PlayerFightLogId = e.Id,
            MechanicName = e.Name,
            MechanicCount = e.Count,
        }).ToList();
}
