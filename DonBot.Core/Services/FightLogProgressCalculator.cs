using DonBot.Core.Models.Enums;
using DonBot.Core.Models.GuildWars2;
using Newtonsoft.Json;

namespace DonBot.Core.Services;

public readonly record struct FightLogProgress(decimal FightPercent, int? FightPhase);

public static class FightLogProgressCalculator
{
    private const int HarvestTemplePhaseCount = 6;
    private const decimal UraHealedPhaseStartPercent = 30m;
    private static readonly string[] HarvestTemplePhaseNames =
    [
        "Jormag",
        "Primordus",
        "Kralkatorrik",
        "Mordremoth",
        "Zhaitan",
        "Soo-Won"
    ];

    public static int ResolveFightMode(FightEliteInsightDataModel fightData, ArcDpsPhase? fightPhase = null)
    {
        if (!string.IsNullOrEmpty(fightData.FightMode) || !string.IsNullOrEmpty(fightPhase?.Mode))
        {
            return (fightData.FightMode ?? fightPhase?.Mode) switch
            {
                "Normal Mode" => 0,
                "Challenge Mode" => 1,
                "Legendary Challenge Mode" => 2,
                _ => fightData.GetFightMode()
            };
        }

        return fightData.LogName?.Split(' ').LastOrDefault() switch
        {
            "CM" => 1,
            "LCM" => 2,
            _ => 0
        };
    }

    public static FightLogProgress Calculate(FightEliteInsightDataModel fightData, short fightType, int fightMode)
    {
        var mainTarget = fightData.Targets?.FirstOrDefault();
        var defaultProgress = new FightLogProgress(GetTargetPercent(mainTarget), null);

        return fightType switch
        {
            (short)FightTypesEnum.Ht => CalculateHarvestTemple(fightData, defaultProgress),
            (short)FightTypesEnum.Ura when IsChallengeMode(fightMode) => CalculateUra(fightData, defaultProgress),
            _ => defaultProgress
        };
    }

    public static bool TryCalculateFromRaw(string? rawFightData, short fightType, int fightMode, out FightLogProgress progress)
    {
        progress = default;
        if (string.IsNullOrWhiteSpace(rawFightData))
        {
            return false;
        }

        try
        {
            var raw = JsonConvert.DeserializeObject<RawProgressFightData>(rawFightData);
            if (raw == null)
            {
                return false;
            }

            var fightData = raw.ToFightData();
            var effectiveFightMode = fightMode != 0
                ? fightMode
                : ResolveFightMode(fightData, fightData.Phases?.FirstOrDefault());

            progress = Calculate(fightData, fightType, effectiveFightMode);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    public static bool HasProgressionNormalization(short fightType) =>
        fightType is (short)FightTypesEnum.Ht or (short)FightTypesEnum.Ura;

    public static decimal NormalizeForProgression(short fightType, int fightMode, decimal fightPercent, int? fightPhase)
    {
        var clampedPercent = ClampPercent(fightPercent);

        return fightType switch
        {
            (short)FightTypesEnum.Ht => NormalizeLinearPhase(clampedPercent, fightPhase, HarvestTemplePhaseCount),
            (short)FightTypesEnum.Ura when ShouldNormalizeUra(fightMode, fightPhase) && fightPhase == 1
                => RoundPercent(UraHealedPhaseStartPercent + clampedPercent * 0.70m),
            (short)FightTypesEnum.Ura when ShouldNormalizeUra(fightMode, fightPhase) && fightPhase >= 2
                => Math.Min(clampedPercent, UraHealedPhaseStartPercent),
            _ => clampedPercent
        };
    }

    private static FightLogProgress CalculateHarvestTemple(FightEliteInsightDataModel fightData, FightLogProgress defaultProgress)
    {
        var bossTargets = fightData.Targets?
            .Where(t => t.HbWidth == 800 && t.Health > 0)
            .ToList();

        if (bossTargets?.Count > 0)
        {
            var phase = Math.Min(bossTargets.Count, HarvestTemplePhaseCount);
            return new FightLogProgress(GetTargetPercent(bossTargets.Last()), phase);
        }

        var phaseTarget = FindLatestHarvestTemplePhaseTarget(fightData);
        if (phaseTarget.Target != null)
        {
            return new FightLogProgress(GetTargetPercent(phaseTarget.Target), phaseTarget.Phase);
        }

        return defaultProgress;
    }

    private static FightLogProgress CalculateUra(FightEliteInsightDataModel fightData, FightLogProgress defaultProgress)
    {
        var reachedHealedPhase = fightData.Phases?.Any(p =>
            string.Equals(p.Name, "Healed", StringComparison.OrdinalIgnoreCase)) == true;

        return defaultProgress with { FightPhase = reachedHealedPhase ? 2 : 1 };
    }

    private static (ArcDpsTarget? Target, int? Phase) FindLatestHarvestTemplePhaseTarget(FightEliteInsightDataModel fightData)
    {
        if (fightData.Targets == null || fightData.Phases == null)
        {
            return (null, null);
        }

        for (var phaseIndex = HarvestTemplePhaseNames.Length - 1; phaseIndex >= 0; phaseIndex--)
        {
            var phaseName = HarvestTemplePhaseNames[phaseIndex];
            var phase = fightData.Phases.LastOrDefault(p =>
                string.Equals(p.Name, phaseName, StringComparison.OrdinalIgnoreCase));

            var targetIndex = phase?.Targets?
                .Where(index => index >= 0 && index < fightData.Targets.Count)
                .Cast<int?>()
                .FirstOrDefault();
            if (targetIndex.HasValue)
            {
                return (fightData.Targets[targetIndex.Value], phaseIndex + 1);
            }
        }

        return (null, null);
    }

    private static decimal NormalizeLinearPhase(decimal fightPercent, int? fightPhase, int totalPhases)
    {
        if (!fightPhase.HasValue || fightPhase.Value < 1)
        {
            return fightPercent;
        }

        var phase = Math.Clamp(fightPhase.Value, 1, totalPhases);
        return RoundPercent(((totalPhases - phase) * 100m + fightPercent) / totalPhases);
    }

    private static decimal GetTargetPercent(ArcDpsTarget? target)
    {
        if (target == null)
        {
            return 100m;
        }

        if (target.Health > 0 && target.HpLeft >= 0)
        {
            return RoundPercent(target.HpLeft / (decimal)target.Health * 100m);
        }

        if (target.Percent is >= 0 and <= 100)
        {
            return RoundPercent((decimal)target.Percent);
        }

        return 0m;
    }

    private static bool IsChallengeMode(int fightMode) => fightMode is 1 or 2;

    private static bool ShouldNormalizeUra(int fightMode, int? fightPhase) =>
        IsChallengeMode(fightMode) || fightPhase.HasValue;

    private static decimal ClampPercent(decimal percent) => Math.Clamp(percent, 0m, 100m);

    private static decimal RoundPercent(decimal percent) => Math.Round(ClampPercent(percent), 2);

    private sealed class RawProgressFightData
    {
        [JsonProperty("targets")]
        public List<RawProgressTarget>? Targets { get; set; }

        [JsonProperty("phases")]
        public List<RawProgressPhase>? Phases { get; set; }

        [JsonProperty("fightMode")]
        public string? FightMode { get; set; }

        [JsonProperty("logName")]
        public string? LogName { get; set; }

        public FightEliteInsightDataModel ToFightData() => new()
        {
            Targets = Targets?.Select(t => new ArcDpsTarget
            {
                HbWidth = t.HbWidth,
                Percent = t.Percent,
                HpLeft = t.HpLeft,
                Name = t.Name,
                Health = t.Health
            }).ToList(),
            Phases = Phases?.Select(p => new ArcDpsPhase
            {
                Name = p.Name ?? string.Empty,
                Start = p.Start,
                End = p.End,
                Targets = p.Targets,
                Mode = p.Mode ?? string.Empty
            }).ToList(),
            FightMode = FightMode,
            LogName = LogName
        };
    }

    private sealed class RawProgressTarget
    {
        [JsonProperty("hbWidth")]
        public int HbWidth { get; set; }

        [JsonProperty("percent")]
        public float Percent { get; set; }

        [JsonProperty("hpLeft")]
        public long HpLeft { get; set; }

        [JsonProperty("name")]
        public string? Name { get; set; }

        [JsonProperty("health")]
        public long Health { get; set; }
    }

    private sealed class RawProgressPhase
    {
        [JsonProperty("name")]
        public string? Name { get; set; }

        [JsonProperty("start")]
        public double Start { get; set; }

        [JsonProperty("end")]
        public double End { get; set; }

        [JsonProperty("targets")]
        public List<int>? Targets { get; set; }

        [JsonProperty("mode")]
        public string? Mode { get; set; }
    }
}
