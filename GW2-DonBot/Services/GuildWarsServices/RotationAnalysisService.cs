using DonBot.Models.Entities;
using DonBot.Models.GuildWars2;
using DonBot.Services.DatabaseServices;

namespace DonBot.Services.GuildWarsServices;

public sealed class RotationAnalysisService(IEntityService entityService) : IRotationAnalysisService
{
    private const double SkillIntervalWindow = 150.0;
    private const int MinConsecutiveCasts = 3;

    public async Task AnalyzePlayerRotations(EliteInsightDataModel data)
    {
        if (data.FightEliteInsightDataModel.Players == null ||
            data.FightEliteInsightDataModel.SkillMap == null ||
            data.FightEliteInsightDataModel.Wvw) // Only analyze PvE logs
        {
            return;
        }

        var anomalies = new List<RotationAnomaly>();
        var fightUrl = data.FightEliteInsightDataModel.Url;

        foreach (var player in data.FightEliteInsightDataModel.Players)
        {
            if (player.Acc == null || player.Name == null || player.Details?.Rotation == null || player.NotInSquad)
            {
                continue;
            }

            var detectedAnomalies = AnalyzePlayerRotation(
                player.Acc,
                player.Name,
                player.Details.Rotation,
                data.FightEliteInsightDataModel.SkillMap,
                fightUrl
            );

            anomalies.AddRange(detectedAnomalies);
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
        var anomalies = new List<RotationAnomaly>();

        // rotation is a nested list: outer list contains rotation phases
        // We check the first list (rotation[0])
        if (rotation.Count == 0 || rotation[0].Count == 0)
        {
            return anomalies;
        }

        var skills = rotation[0];

        // Group skills by skill ID, excluding auto attacks
        var skillCasts = new Dictionary<long, List<double>>();

        foreach (var skill in skills)
        {
            // skill format: [time, skillId, ignored, ignored, ignored]
            if (skill.Count < 2)
            {
                continue;
            }

            var castTime = skill[0];
            var skillId = (long)skill[1];
            var skillKey = $"s{skillId}";

            // Check if this is an auto attack
            if (skillMap.TryGetValue(skillKey, out var skillInfo) && skillInfo.IsAutoAttack)
            {
                continue; // Skip auto attacks
            }

            if (!skillCasts.ContainsKey(skillId))
            {
                skillCasts[skillId] = [];
            }

            skillCasts[skillId].Add(castTime);
        }

        // Analyze each skill for consistent intervals
        foreach (var (skillId, castTimes) in skillCasts)
        {
            if (castTimes.Count < MinConsecutiveCasts)
            {
                continue;
            }

            // Sort cast times
            var sortedCastTimes = castTimes.OrderBy(t => t).ToList();

            // Find sequences of at least 3 consecutive casts with consistent intervals
            var detectedSequences = FindConsistentIntervals(sortedCastTimes);

            foreach (var sequence in detectedSequences)
            {
                var skillKey = $"s{skillId}";
                var skillName = skillMap.TryGetValue(skillKey, out var skillInfo) && !string.IsNullOrEmpty(skillInfo.Name)
                    ? skillInfo.Name
                    : $"Skill {skillId}";

                var anomaly = new RotationAnomaly
                {
                    AccountName = accountName,
                    CharacterName = characterName,
                    SkillId = skillId,
                    SkillName = skillName,
                    ConsecutiveCasts = sequence.Count,
                    AverageInterval = (decimal)sequence.AverageInterval,
                    MaxDeviation = (decimal)sequence.MaxDeviation,
                    Description = $"Detected {sequence.Count} consecutive casts of skill {skillId} " +
                                  $"with avg interval {sequence.AverageInterval:F0}ms " +
                                  $"(max deviation: {sequence.MaxDeviation:F0}ms)",
                    FightUrl = fightUrl,
                    DetectedAt = DateTime.UtcNow
                };

                anomalies.Add(anomaly);
            }
        }

        return anomalies;
    }

    private static List<IntervalSequence> FindConsistentIntervals(List<double> sortedCastTimes)
    {
        var sequences = new List<IntervalSequence>();

        if (sortedCastTimes.Count < MinConsecutiveCasts)
        {
            return sequences;
        }

        for (int i = 0; i <= sortedCastTimes.Count - MinConsecutiveCasts; i++)
        {
            var currentSequence = new List<double> { sortedCastTimes[i] };
            var intervals = new List<double>();

            for (int j = i + 1; j < sortedCastTimes.Count; j++)
            {
                var interval = (sortedCastTimes[j] - sortedCastTimes[j - 1]) * 1000.0; // Convert to ms

                if (currentSequence.Count == 1)
                {
                    // First interval in sequence
                    currentSequence.Add(sortedCastTimes[j]);
                    intervals.Add(interval);
                }
                else
                {
                    // Check if this interval is consistent with the sequence
                    var avgInterval = intervals.Average();
                    var deviation = Math.Abs(interval - avgInterval);

                    if (deviation <= SkillIntervalWindow)
                    {
                        currentSequence.Add(sortedCastTimes[j]);
                        intervals.Add(interval);
                    }
                    else
                    {
                        // Sequence broken
                        break;
                    }
                }
            }

            // If we found a sequence of at least MinConsecutiveCasts, record it
            if (currentSequence.Count >= MinConsecutiveCasts && intervals.Any())
            {
                var avgInterval = intervals.Average();
                var maxDeviation = intervals.Max(x => Math.Abs(x - avgInterval));

                sequences.Add(new IntervalSequence
                {
                    Count = currentSequence.Count,
                    AverageInterval = avgInterval,
                    MaxDeviation = maxDeviation
                });
            }
        }

        return sequences;
    }

    private class IntervalSequence
    {
        public int Count { get; init; }
        public double AverageInterval { get; init; }
        public double MaxDeviation { get; init; }
    }
}
