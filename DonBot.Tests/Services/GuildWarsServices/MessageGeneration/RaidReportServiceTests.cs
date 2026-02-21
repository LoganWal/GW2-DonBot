using DonBot.Models.Entities;
using DonBot.Services.GuildWarsServices.MessageGeneration;
using Xunit.Abstractions;

namespace DonBot.Tests.Services.GuildWarsServices.MessageGeneration;

public class RaidReportServiceTests(ITestOutputHelper output)
{
    [Fact]
    public void BuildSurvivabilityTable_WhenEmpty_StartsWithCodeFenceAndHeader()
    {
        var table = RaidReportService.BuildSurvivabilityTable(Group([]));
        Assert.StartsWith("```Player", table);
    }

    [Fact]
    public void BuildSurvivabilityTable_WhenEmpty_EndsWithCodeFence()
    {
        var table = RaidReportService.BuildSurvivabilityTable(Group([]));
        Assert.EndsWith("```", table);
    }

    [Fact]
    public void BuildSurvivabilityTable_WhenEmpty_HeaderContainsAllColumnNames()
    {
        var table = RaidReportService.BuildSurvivabilityTable(Group([]));
        var header = table.Split('\n')[0];
        Assert.Contains("Player", header);
        Assert.Contains("Res (s)", header);
        Assert.Contains("Dmg Taken", header);
        Assert.Contains("Times Downed", header);
        Assert.Contains("First", header);
    }
    
    [Fact]
    public void BuildSurvivabilityTable_WithMultiplePlayers_DataColumnsAlignWithHeader()
    {
        var logs = new List<PlayerFightLog>
        {
            new() { FightLogId = 1, GuildWarsAccountName = "Alice.1234",    ResurrectionTime = 5000,  DamageTaken = 100_000, TimesDowned = 2, TimeOfDeath = 3000 },
            new() { FightLogId = 1, GuildWarsAccountName = "Bob.5678",      ResurrectionTime = 0,     DamageTaken = 200_000, TimesDowned = 0, TimeOfDeath = 5000 },
            new() { FightLogId = 2, GuildWarsAccountName = "Alice.1234",    ResurrectionTime = 2000,  DamageTaken = 80_000,  TimesDowned = 1, TimeOfDeath = 1000 },
            new() { FightLogId = 2, GuildWarsAccountName = "Bob.5678",      ResurrectionTime = 0,     DamageTaken = 150_000, TimesDowned = 0, TimeOfDeath = null },
            new() { FightLogId = 3, GuildWarsAccountName = "LongName.9999", ResurrectionTime = 10000, DamageTaken = 50_000,  TimesDowned = 3, TimeOfDeath = null },
        };

        var table = RaidReportService.BuildSurvivabilityTable(Group(logs));
        var rawLines = table.Split('\n');

        // The first raw line is "```Player   Res (s)   ..." — strip the opening fence
        var header = rawLines[0].TrimStart('`');

        // Data rows are everything after the header, excluding the closing ``` and empty lines
        var dataLines = rawLines.Skip(1).Where(l => l.Length > 0 && !l.StartsWith("```")).ToList();

        // Find where each column header starts
        var resStart   = header.IndexOf("Res (s)", StringComparison.Ordinal);
        var dmgStart   = header.IndexOf("Dmg Taken", StringComparison.Ordinal);
        var downsStart = header.IndexOf("Times Downed", StringComparison.Ordinal);
        var firstStart = header.IndexOf("First", StringComparison.Ordinal);

        Assert.True(resStart > 0,   "Res (s) column header not found");
        Assert.True(dmgStart > 0,   "Dmg Taken column header not found");
        Assert.True(downsStart > 0, "Times Downed column header not found");
        Assert.True(firstStart > 0, "First column header not found");

        // Columns must be in left-to-right order
        Assert.True(resStart < dmgStart,     "Res (s) must come before Dmg Taken");
        Assert.True(dmgStart < downsStart,   "Dmg Taken must come before Times Downed");
        Assert.True(downsStart < firstStart, "Times Downed must come before First");

        // Every data row must reach at least as far as the First column start position
        foreach (var dataLine in dataLines)
        {
            Assert.True(dataLine.Length >= firstStart, $"Data row is shorter than First column position: '{dataLine}'");
        }
    }
    
    [Fact]
    public void BuildSurvivabilityTable_WhenEachPlayerDiesFirstOnce_BothShowCountOfOne()
    {
        // Fight 1: Alice dies at t=1000, Bob at t=5000 → Alice is first
        // Fight 2: Bob dies at t=2000, Alice at t=8000 → Bob is first
        var logs = new List<PlayerFightLog>
        {
            new() { FightLogId = 1, GuildWarsAccountName = "Alice.1234", TimeOfDeath = 1000, DamageTaken = 10 },
            new() { FightLogId = 1, GuildWarsAccountName = "Bob.5678",   TimeOfDeath = 5000, DamageTaken = 20 },
            new() { FightLogId = 2, GuildWarsAccountName = "Alice.1234", TimeOfDeath = 8000, DamageTaken = 10 },
            new() { FightLogId = 2, GuildWarsAccountName = "Bob.5678",   TimeOfDeath = 2000, DamageTaken = 20 },
        };

        var table = RaidReportService.BuildSurvivabilityTable(Group(logs));

        var aliceLine = table.Split('\n').FirstOrDefault(l => l.TrimStart().StartsWith("Alice.1234"));
        var bobLine   = table.Split('\n').FirstOrDefault(l => l.TrimStart().StartsWith("Bob.5678"));

        Assert.NotNull(aliceLine);
        Assert.NotNull(bobLine);
        Assert.EndsWith("1", aliceLine.TrimEnd());
        Assert.EndsWith("1", bobLine.TrimEnd());
    }

    [Fact]
    public void BuildSurvivabilityTable_WhenPlayerNeverDiesFirst_FirstColumnShowsZero()
    {
        // Only fight: Alice dies first, Charlie never dies
        var logs = new List<PlayerFightLog>
        {
            new() { FightLogId = 1, GuildWarsAccountName = "Alice.1234",   TimeOfDeath = 1000, DamageTaken = 10 },
            new() { FightLogId = 1, GuildWarsAccountName = "Charlie.0000", TimeOfDeath = null, DamageTaken = 20 },
        };

        var table = RaidReportService.BuildSurvivabilityTable(Group(logs));
        var charlieLine = table.Split('\n').FirstOrDefault(l => l.TrimStart().StartsWith("Charlie.0000"));

        Assert.NotNull(charlieLine);
        Assert.EndsWith("0", charlieLine.TrimEnd());
    }

    [Fact]
    public void BuildSurvivabilityTable_WhenNoOnesDies_FirstColumnIsZeroForAllPlayers()
    {
        var logs = new List<PlayerFightLog>
        {
            new() { FightLogId = 1, GuildWarsAccountName = "Alice.1234", TimeOfDeath = null, DamageTaken = 10 },
            new() { FightLogId = 1, GuildWarsAccountName = "Bob.5678",   TimeOfDeath = null, DamageTaken = 20 },
        };

        var table = RaidReportService.BuildSurvivabilityTable(Group(logs));
        var dataLines = table.Split('\n').Skip(1).Where(l => !l.StartsWith("```") && l.Length > 0).ToList();

        foreach (var line in dataLines)
        {
            Assert.EndsWith("0", line.TrimEnd());
        }
    }

    [Fact]
    public void BuildSurvivabilityTable_WhenPlayerDiesFirstInMultipleFights_CountAccumulates()
    {
        // Alice is first to die in all 3 fights
        var logs = new List<PlayerFightLog>
        {
            new() { FightLogId = 1, GuildWarsAccountName = "Alice.1234", TimeOfDeath = 500,  DamageTaken = 10 },
            new() { FightLogId = 1, GuildWarsAccountName = "Bob.5678",   TimeOfDeath = 9000, DamageTaken = 20 },
            new() { FightLogId = 2, GuildWarsAccountName = "Alice.1234", TimeOfDeath = 300,  DamageTaken = 10 },
            new() { FightLogId = 2, GuildWarsAccountName = "Bob.5678",   TimeOfDeath = 7000, DamageTaken = 20 },
            new() { FightLogId = 3, GuildWarsAccountName = "Alice.1234", TimeOfDeath = 100,  DamageTaken = 10 },
            new() { FightLogId = 3, GuildWarsAccountName = "Bob.5678",   TimeOfDeath = null, DamageTaken = 20 },
        };

        var table = RaidReportService.BuildSurvivabilityTable(Group(logs));
        var aliceLine = table.Split('\n').FirstOrDefault(l => l.TrimStart().StartsWith("Alice.1234"));

        Assert.NotNull(aliceLine);
        Assert.EndsWith("3", aliceLine.TrimEnd());
    }
    
    [Fact]
    public void BuildSurvivabilityTable_WithMultiplePlayers_OrderedAscendingByDamageTaken()
    {
        var logs = new List<PlayerFightLog>
        {
            new() { FightLogId = 1, GuildWarsAccountName = "HighDmg.1111", DamageTaken = 500_000 },
            new() { FightLogId = 1, GuildWarsAccountName = "LowDmg.2222",  DamageTaken = 50_000  },
            new() { FightLogId = 1, GuildWarsAccountName = "MidDmg.3333",  DamageTaken = 200_000 },
        };

        var table = RaidReportService.BuildSurvivabilityTable(Group(logs));
        var dataLines = table.Split('\n')
            .Skip(1) // header is line 0 (starts with ```)
            .Where(l => !l.StartsWith("```") && l.Length > 0)
            .ToList();

        Assert.Equal(3, dataLines.Count);
        Assert.Contains("LowDmg",  dataLines[0]);
        Assert.Contains("MidDmg",  dataLines[1]);
        Assert.Contains("HighDmg", dataLines[2]);
    }
    
    [Fact]
    public void BuildSurvivabilityTable_WithResurrectionTime_ConvertedFromMillisecondsToSeconds()
    {
        var logs = new List<PlayerFightLog>
        {
            new() { FightLogId = 1, GuildWarsAccountName = "Alice.1234", ResurrectionTime = 7500, DamageTaken = 0 },
        };

        var table = RaidReportService.BuildSurvivabilityTable(Group(logs));
        // 7500 ms = 7.5 s
        Assert.Contains("7.5", table);
    }

    [Fact]
    public void BuildSurvivabilityTable_WithLongAccountName_ClippedToThirteenCharacters()
    {
        var logs = new List<PlayerFightLog>
        {
            new() { FightLogId = 1, GuildWarsAccountName = "VeryLongAccountName.9999", DamageTaken = 0 },
        };

        var table = RaidReportService.BuildSurvivabilityTable(Group(logs));
        var dataLine = table.Split('\n').FirstOrDefault(l => l.Contains("VeryLong"));

        Assert.NotNull(dataLine);
        Assert.DoesNotContain("VeryLongAccountName", dataLine);
        Assert.Contains("VeryLongAccou", dataLine);
    }

    // Visual output (not a pass/fail assertion — prints to console for review)
    [Fact]
    public void BuildSurvivabilityTable_WithRealisticData_PrintsFormattedTable()
    {
        var logs = new List<PlayerFightLog>
        {
            // Fight 1
            new() { FightLogId = 1, GuildWarsAccountName = "Alice.1234",       ResurrectionTime = 5000,  DamageTaken = 120_000, TimesDowned = 2, TimeOfDeath = 3000  },
            new() { FightLogId = 1, GuildWarsAccountName = "Bob.5678",          ResurrectionTime = 0,     DamageTaken = 250_000, TimesDowned = 0, TimeOfDeath = 10000 },
            new() { FightLogId = 1, GuildWarsAccountName = "Charlie.0000",      ResurrectionTime = 12000, DamageTaken = 80_000,  TimesDowned = 4, TimeOfDeath = 1500  },
            new() { FightLogId = 1, GuildWarsAccountName = "DeeplyNested.1111", ResurrectionTime = 0,     DamageTaken = 310_000, TimesDowned = 0, TimeOfDeath = null  },
            // Fight 2
            new() { FightLogId = 2, GuildWarsAccountName = "Alice.1234",       ResurrectionTime = 2000,  DamageTaken = 95_000,  TimesDowned = 1, TimeOfDeath = 8000  },
            new() { FightLogId = 2, GuildWarsAccountName = "Bob.5678",          ResurrectionTime = 7000,  DamageTaken = 175_000, TimesDowned = 1, TimeOfDeath = 2000  },
            new() { FightLogId = 2, GuildWarsAccountName = "Charlie.0000",      ResurrectionTime = 0,     DamageTaken = 60_000,  TimesDowned = 0, TimeOfDeath = null  },
            new() { FightLogId = 2, GuildWarsAccountName = "DeeplyNested.1111", ResurrectionTime = 0,     DamageTaken = 200_000, TimesDowned = 0, TimeOfDeath = null  },
            // Fight 3
            new() { FightLogId = 3, GuildWarsAccountName = "Alice.1234",       ResurrectionTime = 0,     DamageTaken = 70_000,  TimesDowned = 0, TimeOfDeath = 500   },
            new() { FightLogId = 3, GuildWarsAccountName = "Bob.5678",          ResurrectionTime = 0,     DamageTaken = 190_000, TimesDowned = 0, TimeOfDeath = null  },
            new() { FightLogId = 3, GuildWarsAccountName = "Charlie.0000",      ResurrectionTime = 3000,  DamageTaken = 110_000, TimesDowned = 2, TimeOfDeath = 4000  },
            new() { FightLogId = 3, GuildWarsAccountName = "DeeplyNested.1111", ResurrectionTime = 0,     DamageTaken = 280_000, TimesDowned = 0, TimeOfDeath = null  },
        };

        var table = RaidReportService.BuildSurvivabilityTable(Group(logs));

        var display = table.Replace("```", string.Empty).Trim();
        output.WriteLine(string.Empty);
        output.WriteLine("=== Survivability Table (visual check) ===");
        output.WriteLine(display);
        output.WriteLine("==========================================");

        Assert.True(true);
    }
    
    private static List<IGrouping<string, PlayerFightLog>> Group(IEnumerable<PlayerFightLog> logs) =>
        logs.GroupBy(l => l.GuildWarsAccountName).ToList();
}
