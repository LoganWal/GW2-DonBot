using DonBot.Extensions;
using DonBot.Services.GuildWarsServices;
using DonBot.Services.GuildWarsServices.MessageGeneration;
using Xunit.Abstractions;

namespace DonBot.Tests.Services.GuildWarsServices.MessageGeneration;

// Renders every report table's real column layout with realistic sample data so the formatting
// can be eyeballed, and asserts no row exceeds DiscordTable.MaxRowWidth (the width at which
// Discord's mobile code blocks wrap the last column onto its own line).
public class TableLayoutPreviewTests(ITestOutputHelper output)
{
    [Fact]
    public void AllReportTables_RenderWithinMobileWidth()
    {
        Preview("PvE Raid - Fights Overview", RaidReportService.FightsColumns,
            ["(LCM)Dhuum", "8m:12s", "9m:40s", "16"],
            ["(CM)Sabetha", "5m:03s", "5m:03s (!)", "4"],
            ["(NM)Ura", "None", "19m:40s", "7"]);

        Preview("PvE Raid - Player Overview", RaidReportService.PlayerColumns,
            ["Doubleu.3540", "29.6K/s", "5.8K/s", "97.9", "98.2"],
            ["SliferAlpha.9", "25.1K/s", "11.1K/s", "98.1", "93.9"],
            ["Naro.9250", "1.4K/s", "357.1/s", "77.1", "99.0"]);

        Preview("PvE Raid - Survivability", RaidReportService.SurvivabilityColumns,
            ["Aerell.8473", "1.151", "4326134", "8", "6"],
            ["WalmsLo.8437", "13.101", "3754016", "7", "2"],
            ["Naro.9250", "30.528", "2997453", "8", "2"]);

        Preview("PvE Raid - Aggregate WvW Raid Overview", RaidReportService.WvWRaidColumns,
            ["50", "120", "340", "85", "12"]);

        Preview("PvE Raid - Aggregate WvW Sub Overview", RaidReportService.WvWSubColumns,
            ["1", "98.5", "97.92", "14"],
            ["2", "12.34", "5.23", "3"]);

        Preview("WvW Fight - Damage", WvWFightSummaryService.DamageColumns,
            ["01", "Doubleu.3540 (Fir)", "29.6K", "5.8K"],
            ["02", "SliferAlpha.98 (Scr)", "25.1K", "11.1K"]);

        Preview("WvW Fight - Cleanses", WvWFightSummaryService.CleanseColumns,
            ["01", "WalmsLo.8437 (Tem)", "1340"]);

        Preview("WvW Fight - Stab", WvWFightSummaryService.StabColumns,
            ["01", "Aerell.8473 (Gua)", "1", "12.34", "5.23"]);

        Preview("WvW Fight - Healing", WvWFightSummaryService.HealingColumns,
            ["01", "Renero.9172 (Dru)", "999.9K"]);

        Preview("WvW Fight - Distance", WvWFightSummaryService.DistanceColumns,
            ["01", "Monty.8103 (Spe)", "342.50"]);

        Preview("WvW Fight - Friendly/Stream", WvWFightSummaryService.FriendlyColumns,
            ["Ally", "60(45)", "12.4M", "210.5K", "85", "12"],
            ["Foe", "72", "9.8M", "166.1K", "120", "48"]);

        Preview("WvW Leaderboard - Damage", WeeklyLeaderboardService.LeaderDamageColumns,
            ["01", "(12) Doubleu.3540", "999.9M", "120.4M"]);

        Preview("WvW Leaderboard - Stab", WeeklyLeaderboardService.LeaderStabColumns,
            ["01", "(11) Aerell.8473", "98.50", "12.34"]);

        Preview("Leaderboard - PvE Cleave DPS", WeeklyLeaderboardService.SimpleColumns("Avg Cleave/s"),
            ["01", "(8) SliferAlpha.99", "11.1K/s"]);

        Preview("Leaderboard - PvE Dmg Taken", WeeklyLeaderboardService.SimpleColumns("Avg Dmg Taken"),
            ["01", "(8) WalmsLo.8437", "4.3M"]);

        Preview("Know My Enemy", FightLogService.EnemyColumns,
            ["Spellbreaker", "8", "1.2M", "980.4K", "240.1K"],
            ["Firebrand", "5", "640.5K", "12.3K", "628.2K"]);
    }

    private void Preview(string title, IReadOnlyList<DiscordTable.Column> columns, params string[][] rows)
    {
        var header = DiscordTable.Header(columns);
        var body = string.Concat(rows.Select(r => DiscordTable.Row(columns, r)));
        var table = $"```{header}{body}```";

        output.WriteLine($"=== {title} ===");
        output.WriteLine(table);
        output.WriteLine(string.Empty);

        foreach (var line in table.Replace("```", string.Empty).Split('\n').Where(l => l.Length > 0))
        {
            Assert.True(line.Length <= DiscordTable.MaxRowWidth,
                $"[{title}] line exceeds {DiscordTable.MaxRowWidth} chars ({line.Length}): '{line}'");
        }
    }
}
