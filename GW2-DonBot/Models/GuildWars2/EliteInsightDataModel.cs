using Newtonsoft.Json;

namespace DonBot.Models.GuildWars2
{
    public class EliteInsightDataModel
    {
        [JsonProperty("url")]
        public string Url { get; set; } = string.Empty;

        [JsonProperty("targets")]
        public List<ArcDpsTarget>? Targets { get; set; }

        [JsonProperty("players")]
        public List<ArcDpsPlayer>? Players { get; set; }

        [JsonProperty("phases")]
        public List<ArcDpsPhase>? Phases { get; set; }

        [JsonProperty("mechanicMap")]
        public List<MechanicMap>? MechanicMap { get; set; }

        [JsonProperty("healingStatsExtension")]
        public HealingStatsExtension? HealingStatsExtension { get; set; }

        [JsonProperty("barrierStatsExtension")]
        public BarrierStatsExtension? BarrierStatsExtension { get; set; }

        [JsonProperty("encounterDuration")]
        public string? EncounterDuration { get; set; }

        [JsonProperty("success")]
        public bool Success { get; set; }

        [JsonProperty("wvw")]
        public bool Wvw { get; set; }

        [JsonProperty("fightName")]
        public string? FightName { get; set; }

        [JsonProperty("fightMode")]
        public string FightMode { get; set; } = string.Empty;

        [JsonProperty("encounterStart")]
        public string EncounterStart { get; set; } = string.Empty;

        [JsonProperty("encounterEnd")]
        public string EncounterEnd { get; set; } = string.Empty;

        [JsonProperty("encounterID")]
        public long EncounterId { get; set; }

        public int GetFightMode()
        {
            return FightMode switch
            {
                "Normal Mode" => 0,
                "Challenge Mode" => 1,
                "Legendary Challenge Mode" => 2,
                _ => 0
            };
        }
    }

    public class BarrierStatsExtension
    {
        [JsonProperty("barrierPhases")]
        public List<BarrierPhase>? BarrierPhases { get; set; }
    }

    public class BarrierPhase
    {
        [JsonProperty("outgoingBarrierStats")]
        public List<List<long>>? OutgoingBarrierStats { get; set; }
    }

    public class HealingStatsExtension
    {
        [JsonProperty("healingPhases")]
        public List<HealingPhase>? HealingPhases { get; set; }
    }

    public class HealingPhase
    {
        [JsonProperty("outgoingHealingStatsTargets")]
        public List<List<List<long>>>? OutgoingHealingStatsTargets { get; set; }
    }

    public class MechanicMap
    {
        [JsonProperty("name")]
        public string? Name { get; set; }

        [JsonProperty("playerMech")]
        public bool PlayerMech { get; set; }
    }

    public class ArcDpsPhase
    {
        [JsonProperty("duration")]
        public long Duration { get; set; }

        [JsonProperty("dpsStats")]
        public List<List<long>>? DpsStats { get; set; }

        [JsonProperty("dpsStatsTargets")]
        public List<List<List<long>>>? DpsStatsTargets { get; set; }

        [JsonProperty("offensiveStatsTargets")]
        public List<List<List<long>>>? OffensiveStatsTargets { get; set; }

        [JsonProperty("offensiveStats")]
        public List<List<long>>? OffensiveStats { get; set; }

        [JsonProperty("gameplayStats")]
        public List<List<double>>? GameplayStats { get; set; }

        [JsonProperty("defStats")]
        public List<List<DefStat>>? DefStats { get; set; }

        [JsonProperty("supportStats")]
        public List<List<double>>? SupportStats { get; set; }

        [JsonProperty("buffsStatContainer")]
        public BuffsStatContainer BuffsStatContainer { get; set; } = new();

        [JsonProperty("mechanicStats")]
        public List<List<object>>? MechanicStats { get; set; }
    }
    
    public class BuffsStatContainer
    {
        [JsonProperty("boonStats")]
        public List<BoonActiveStat>? BoonStats { get; set; }

        [JsonProperty("boonGenGroupStats")]
        public List<BoonActiveStat>? BoonGenGroupStats { get; set; }

        [JsonProperty("boonGenOGroupStats")]
        public List<BoonActiveStat>? BoonGenOGroupStats { get; set; }

        [JsonProperty("boonActiveStats")]
        public List<BoonActiveStat>? BoonActiveStats { get; set; }
    }

    public class BoonActiveStat
    {
        [JsonProperty("data")]
        public List<List<double>>? Data { get; set; }
    }

    public class ArcDpsPlayer
    {
        [JsonProperty("group")]
        public long Group { get; set; }

        [JsonProperty("acc")]
        public string? Acc { get; set; }

        [JsonProperty("profession")]
        public string? Profession { get; set; }

        [JsonProperty("notInSquad")]
        public bool NotInSquad { get; set; }

        [JsonProperty("name")]
        public string? Name { get; set; }

        [JsonProperty("details")]
        public ArcsDpsPlayerDetails? Details { get; set; }
    }

    public class ArcsDpsPlayerDetails
    {
        [JsonProperty("rotation")]
        public List<List<List<double>>>? Rotation { get; set; }
    }

    public class DmgDistribution
    {
        [JsonProperty("contributedDamage")]
        public long ContributedDamage { get; set; }

        [JsonProperty("totalDamage")]
        public long TotalDamage { get; set; }

        [JsonProperty("distribution", NullValueHandling = NullValueHandling.Ignore)]
        public List<List<Distribution>>? Distribution { get; set; }
    }

    public class ArcDpsTarget
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

        [JsonProperty("details")]
        public TargetDetails? Details { get; set; }
    }

    public class TargetDetails
    {
        [JsonProperty("dmgDistributions")]
        public List<DmgDistribution>? DmgDistributions { get; set; }
    }

    public struct Distribution
    {
        public bool? Bool;
        public double? Double;

        public static implicit operator Distribution(bool @bool) => new Distribution { Bool = @bool };
        public static implicit operator Distribution(double @double) => new Distribution { Double = @double };
    }

    public struct DefStat
    {
        public double? Double;
        public string String;

        public static implicit operator DefStat(double @double) => new DefStat { Double = @double };
        public static implicit operator DefStat(string @string) => new DefStat { String = @string };
    }
}
