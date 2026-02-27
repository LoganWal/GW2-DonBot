using DonBot.Models.Entities;
using DonBot.Models.GuildWars2;
using DonBot.Services.DatabaseServices;

namespace DonBot.Services.GuildWarsServices;

public sealed class RotationAnalysisService(IEntityService entityService) : IRotationAnalysisService
{
    // CV-based single-skill detection
    private const double CvThreshold = 0.08;         // flag if stddev/mean < 8%
    private const double MinMeanIntervalMs = 500.0;  // ignore skills cast faster than 500ms on average (chains/procs)
    private const int MinCastCount = 4;              // minimum casts needed to evaluate

    // Repeating rotation cycle detection
    private const int MinCycleLength = 3;            // minimum skills in a cycle
    private const int MaxCycleLength = 8;            // maximum skills in a cycle
    private const int MinCycleRepeats = 3;           // minimum consecutive full repeats to flag

    public async Task AnalyzePlayerRotations(EliteInsightDataModel data)
    {
        if (data.FightEliteInsightDataModel.Players == null ||
            data.FightEliteInsightDataModel.SkillMap == null ||
            data.FightEliteInsightDataModel.Wvw)
        {
            return;
        }

        var anomalies = new List<RotationAnomaly>();
        var fightUrl = data.FightEliteInsightDataModel.Url;

        foreach (var player in data.FightEliteInsightDataModel.Players)
        {
            if (player.Acc == null || player.Name == null || player.Details?.Rotation == null || player.NotInSquad)
                continue;

            anomalies.AddRange(AnalyzePlayerRotation(
                player.Acc,
                player.Name,
                player.Details.Rotation,
                data.FightEliteInsightDataModel.SkillMap,
                fightUrl
            ));
        }

        if (anomalies.Any())
        {
            await entityService.RotationAnomaly.AddRangeAsync(anomalies);
        }
    }

    private static List<RotationAnomaly> AnalyzePlayerRotation(
        string accountName,
        string characterName,
        List<List<List<double>>> rotation,
        Dictionary<string, SkillMapEntry> skillMap,
        string fightUrl)
    {
        if (rotation.Count == 0 || rotation[0].Count == 0)
            return [];

        var skillCasts = new Dictionary<long, List<double>>();
        var orderedSkills = new List<(double Time, long SkillId)>();

        foreach (var skill in rotation[0])
        {
            if (skill.Count < 2) continue;

            var castTime = skill[0];
            var skillId = (long)skill[1];

            if (skillMap.TryGetValue($"s{skillId}", out var entry) && entry.IsAutoAttack)
                continue;

            if (!skillCasts.ContainsKey(skillId))
                skillCasts[skillId] = [];

            skillCasts[skillId].Add(castTime);
            orderedSkills.Add((castTime, skillId));
        }

        var anomalies = new List<RotationAnomaly>();

        // Flags skills where cast intervals are suspiciously consistent across all casts,
        // regardless of whether the deviations are sequential or not.
        foreach (var (skillId, castTimes) in skillCasts)
        {
            if (castTimes.Count < MinCastCount) continue;

            var sortedTimes = castTimes.OrderBy(t => t).ToList();
            var intervals = Enumerable.Range(1, sortedTimes.Count - 1)
                .Select(i => (sortedTimes[i] - sortedTimes[i - 1]) * 1000.0)
                .ToList();

            var mean = intervals.Average();
            if (mean < MinMeanIntervalMs) continue;

            var stdDev = Math.Sqrt(intervals.Average(x => Math.Pow(x - mean, 2)));
            var cv = stdDev / mean;

            if (cv >= CvThreshold) continue;

            var skillName = skillMap.TryGetValue($"s{skillId}", out var info) && !string.IsNullOrEmpty(info.Name)
                ? info.Name
                : $"Skill {skillId}";

            anomalies.Add(new RotationAnomaly
            {
                AccountName = accountName,
                CharacterName = characterName,
                SkillId = skillId,
                SkillName = skillName,
                ConsecutiveCasts = castTimes.Count,
                AverageInterval = (decimal)Math.Round(mean, 2),
                MaxDeviation = (decimal)Math.Round(cv * 100, 2),
                Description = $"[Tier 1] {skillName} cast {castTimes.Count}x with avg interval {mean:F0}ms and CV {cv * 100:F1}% (threshold: {CvThreshold * 100:F0}%)",
                FightUrl = fightUrl,
                DetectedAt = DateTime.UtcNow
            });
        }

        // Finds the best repeating N-skill sequence in the ordered cast list and checks whether
        // the time each cycle takes is also suspiciously consistent (CV < threshold).
        // This separates skilled human players (who repeat the same rotation but with natural
        // timing variance from mechanics, movement, and reaction time) from bots/macros
        // (who execute both the same sequence AND the same duration on a fixed timer).
        // A human flagged by Tier 1 alone may just be practicing a good rotation — Tier 2
        // requires the full cycle duration to also be inhuman before flagging.
        var sortedSkills = orderedSkills.OrderBy(s => s.Time).ToList();
        var skillIdSequence = sortedSkills.Select(s => s.SkillId).ToList();
        var skillTimeSequence = sortedSkills.Select(s => s.Time).ToList();

        var cycle = DetectRotationCycle(skillIdSequence, skillTimeSequence, skillMap);
        if (cycle != null)
        {
            anomalies.Add(new RotationAnomaly
            {
                AccountName = accountName,
                CharacterName = characterName,
                SkillId = 0,
                SkillName = cycle.CycleDescription,
                ConsecutiveCasts = cycle.Repeats,
                AverageInterval = (decimal)Math.Round(cycle.AvgCycleTimeMs, 2),
                MaxDeviation = cycle.CycleLength,
                Description = $"[Tier 2] {cycle.CycleLength}-skill rotation repeated {cycle.Repeats}x (avg cycle {cycle.AvgCycleTimeMs:F0}ms, CV {cycle.CycleCv * 100:F1}%): {cycle.CycleDescription}",
                FightUrl = fightUrl,
                DetectedAt = DateTime.UtcNow
            });
        }

        return anomalies;
    }

    // Finds the highest-scoring repeating skill sequence (score = repeats × cycleLength).
    // Tries all cycle lengths from MinCycleLength to MaxCycleLength and all starting positions.
    // Only considers a candidate valid if the cycle duration CV is also below CvThreshold,
    // filtering out human players who follow the same rotation but with natural timing variance.
    private static CycleDetection? DetectRotationCycle(
        List<long> skillIds,
        List<double> skillTimes,
        Dictionary<string, SkillMapEntry> skillMap)
    {
        if (skillIds.Count < MinCycleLength * MinCycleRepeats)
            return null;

        CycleDetection? best = null;

        for (int cycleLen = MinCycleLength; cycleLen <= Math.Min(MaxCycleLength, skillIds.Count / MinCycleRepeats); cycleLen++)
        {
            for (int start = 0; start <= skillIds.Count - cycleLen * MinCycleRepeats; start++)
            {
                var window = skillIds.GetRange(start, cycleLen);
                var repeats = 1;

                while (start + (repeats + 1) * cycleLen <= skillIds.Count)
                {
                    var next = skillIds.GetRange(start + repeats * cycleLen, cycleLen);
                    if (!window.SequenceEqual(next)) break;
                    repeats++;
                }

                if (repeats < MinCycleRepeats) continue;

                // Measure how consistent the cycle duration is across repeats.
                // skillTimes are in seconds (from EliteInsights), converted to ms here.
                var cycleTimes = Enumerable.Range(0, repeats - 1)
                    .Select(r => (skillTimes[start + (r + 1) * cycleLen] - skillTimes[start + r * cycleLen]) * 1000.0)
                    .ToList();

                if (!cycleTimes.Any()) continue;

                var avgCycleTime = cycleTimes.Average();
                var cycleStdDev = Math.Sqrt(cycleTimes.Average(x => Math.Pow(x - avgCycleTime, 2)));
                var cycleCv = cycleStdDev / avgCycleTime;

                // A human following a rotation will have natural variance in cycle timing
                // (~150–300ms jitter from mechanics, movement, and reaction time).
                // Only flag if the cycle duration itself is also suspiciously consistent.
                if (cycleCv >= CvThreshold) continue;

                // Prefer the candidate with the highest score (repeats × cycleLength).
                if (best != null && repeats * cycleLen <= best.Repeats * best.CycleLength)
                    continue;

                var cycleDesc = string.Join(" > ", window.Select(id =>
                {
                    var key = $"s{id}";
                    return skillMap.TryGetValue(key, out var e) && !string.IsNullOrEmpty(e.Name)
                        ? e.Name
                        : $"Skill {id}";
                }));

                best = new CycleDetection
                {
                    CycleLength = cycleLen,
                    Repeats = repeats,
                    AvgCycleTimeMs = avgCycleTime,
                    CycleCv = cycleCv,
                    CycleDescription = cycleDesc
                };
            }
        }

        return best;
    }

    private sealed class CycleDetection
    {
        public int CycleLength { get; init; }
        public int Repeats { get; init; }
        public double AvgCycleTimeMs { get; init; }
        public double CycleCv { get; init; }
        public string CycleDescription { get; init; } = string.Empty;
    }
}
