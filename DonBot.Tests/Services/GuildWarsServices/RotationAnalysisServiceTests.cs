using DonBot.Models.GuildWars2;
using DonBot.Services.GuildWarsServices;

namespace DonBot.Tests.Services.GuildWarsServices;

public class RotationAnalysisServiceTests
{
    private const string Account = "Player.1234";
    private const string Character = "Char";
    private const string Url = "https://b.dps.report/abc";

    /// Build a rotation in EI format: [phase][skill][[time, skillId, ...]]. Time is seconds.
    private static List<List<List<double>>> Rotation(params (double TimeSec, long SkillId)[] casts) =>
    [
        casts.Select(c => new List<double> { c.TimeSec, c.SkillId }).ToList()
    ];

    private static Dictionary<string, SkillMapEntry> SkillMap(params (long Id, string Name, bool IsAuto)[] entries) =>
        entries.ToDictionary(e => $"s{e.Id}", e => new SkillMapEntry { Id = e.Id, Name = e.Name, IsAutoAttack = e.IsAuto });

    private static Dictionary<string, SkillMapEntry> EmptySkillMap() => new();

    [Fact]
    public void AnalyzePlayerRotation_NoCasts_NoAnomalies()
    {
        var anomalies = RotationAnalysisService.AnalyzePlayerRotation(
            Account, Character, [], EmptySkillMap(), Url);
        Assert.Empty(anomalies);
    }

    [Fact]
    public void AnalyzePlayerRotation_FewerThanFourCastsOfSameSkill_NotFlaggedTier1()
    {
        // Only 3 casts of same skill: below MinCastCount (4)
        var rotation = Rotation((1, 1), (2, 1), (3, 1));
        var anomalies = RotationAnalysisService.AnalyzePlayerRotation(
            Account, Character, rotation, EmptySkillMap(), Url);
        Assert.Empty(anomalies);
    }

    [Fact]
    public void AnalyzePlayerRotation_AutoAttacksIgnored()
    {
        // 5 perfectly-spaced casts of an auto-attack should NOT be flagged
        var rotation = Rotation((1, 1), (2, 1), (3, 1), (4, 1), (5, 1));
        var skillMap = SkillMap((1, "AA", true));

        var anomalies = RotationAnalysisService.AnalyzePlayerRotation(
            Account, Character, rotation, skillMap, Url);

        Assert.Empty(anomalies);
    }

    [Fact]
    public void AnalyzePlayerRotation_FastChainSkills_BelowMinMeanInterval_NotFlagged()
    {
        // 5 casts at 100ms intervals - mean 100ms below 500ms threshold (proc/chain), should NOT flag
        var rotation = Rotation((1.0, 1), (1.1, 1), (1.2, 1), (1.3, 1), (1.4, 1));
        var anomalies = RotationAnalysisService.AnalyzePlayerRotation(
            Account, Character, rotation, EmptySkillMap(), Url);
        Assert.Empty(anomalies);
    }

    [Fact]
    public void AnalyzePlayerRotation_PerfectlySpacedSkill_FlaggedTier1()
    {
        // 5 casts spaced exactly 1s apart -> CV = 0 -> flagged
        var rotation = Rotation((1, 5), (2, 5), (3, 5), (4, 5), (5, 5));
        var skillMap = SkillMap((5, "Suspect Skill", false));

        var anomalies = RotationAnalysisService.AnalyzePlayerRotation(
            Account, Character, rotation, skillMap, Url);

        Assert.Single(anomalies);
        Assert.Equal(Account, anomalies[0].AccountName);
        Assert.Equal("Suspect Skill", anomalies[0].SkillName);
        Assert.Equal(5, anomalies[0].SkillId);
        Assert.Equal(5, anomalies[0].ConsecutiveCasts);
        Assert.Contains("Tier 1", anomalies[0].Description);
        Assert.Equal(Url, anomalies[0].FightUrl);
    }

    [Fact]
    public void AnalyzePlayerRotation_HumanIrregularSpacing_NotFlagged()
    {
        // 5 casts with high variance: 1.0, 2.5, 3.1, 5.2, 6.0, CV well above 8%
        var rotation = Rotation((1.0, 5), (2.5, 5), (3.1, 5), (5.2, 5), (6.0, 5));
        var skillMap = SkillMap((5, "Skill", false));

        var anomalies = RotationAnalysisService.AnalyzePlayerRotation(
            Account, Character, rotation, skillMap, Url);

        Assert.Empty(anomalies);
    }

    [Fact]
    public void AnalyzePlayerRotation_SkillNotInMap_FallsBackToSkillIdName()
    {
        var rotation = Rotation((1, 7), (2, 7), (3, 7), (4, 7), (5, 7));
        var anomalies = RotationAnalysisService.AnalyzePlayerRotation(
            Account, Character, rotation, EmptySkillMap(), Url);

        Assert.Single(anomalies);
        Assert.Equal("Skill 7", anomalies[0].SkillName);
    }

    [Fact]
    public void AnalyzePlayerRotation_RepeatingThreeSkillCycleWithStableTiming_FlaggedTier2()
    {
        // 3-skill rotation [1,2,3] repeated 4 times at 5s cycle, CV = 0
        // Each cycle: skill 1 at start, 2 at +1s, 3 at +2s; cycle period 5s
        var casts = new List<(double, long)>();
        for (int cycle = 0; cycle < 4; cycle++)
        {
            casts.Add((cycle * 5.0 + 0.0, 1));
            casts.Add((cycle * 5.0 + 1.0, 2));
            casts.Add((cycle * 5.0 + 2.0, 3));
        }
        var rotation = Rotation([.. casts]);
        var skillMap = SkillMap((1, "A", false), (2, "B", false), (3, "C", false));

        var anomalies = RotationAnalysisService.AnalyzePlayerRotation(
            Account, Character, rotation, skillMap, Url);

        // Tier 2 cycle anomaly should be present (Tier 1 may or may not also fire depending on intervals)
        var cycleAnomaly = anomalies.FirstOrDefault(a => a.Description.Contains("Tier 2"));
        Assert.NotNull(cycleAnomaly);
        Assert.Equal("A > B > C", cycleAnomaly!.SkillName);
        Assert.Equal(4, cycleAnomaly.ConsecutiveCasts);
    }

    [Fact]
    public void AnalyzePlayerRotation_RepeatingCycleWithJitteredTiming_NotFlaggedTier2()
    {
        // Same sequence but cycle durations vary substantially -> Tier 2 should not flag
        // Cycles: 5s, 7s, 5.5s, 8s
        var rotation = Rotation(
            (0, 1), (1, 2), (2, 3),
            (5, 1), (6, 2), (7, 3),
            (12, 1), (13, 2), (14, 3),
            (17.5, 1), (18.5, 2), (19.5, 3));
        var skillMap = SkillMap((1, "A", false), (2, "B", false), (3, "C", false));

        var anomalies = RotationAnalysisService.AnalyzePlayerRotation(
            Account, Character, rotation, skillMap, Url);

        Assert.DoesNotContain(anomalies, a => a.Description.Contains("Tier 2"));
    }

    [Fact]
    public void AnalyzePlayerRotation_OnlyTwoCyclesOfSequence_NotFlaggedTier2()
    {
        // MinCycleRepeats = 3; only 2 repeats should not flag
        var rotation = Rotation(
            (0, 1), (1, 2), (2, 3),
            (5, 1), (6, 2), (7, 3));
        var skillMap = SkillMap((1, "A", false), (2, "B", false), (3, "C", false));

        var anomalies = RotationAnalysisService.AnalyzePlayerRotation(
            Account, Character, rotation, skillMap, Url);

        Assert.DoesNotContain(anomalies, a => a.Description.Contains("Tier 2"));
    }
}
