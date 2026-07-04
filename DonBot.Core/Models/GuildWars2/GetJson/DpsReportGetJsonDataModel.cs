using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace DonBot.Core.Models.GuildWars2.GetJson;

public sealed class DpsReportGetJsonDataModel
{
    [JsonProperty("error")]
    public JToken? Error { get; init; }

    [JsonProperty("players")]
    public List<GetJsonPlayer>? Players { get; init; }

    [JsonProperty("targets")]
    public List<GetJsonTarget>? Targets { get; init; }

    [JsonProperty("phases")]
    public List<GetJsonPhase>? Phases { get; init; }

    [JsonProperty("mechanics")]
    public List<GetJsonMechanic>? Mechanics { get; init; }

    [JsonProperty("buffMap")]
    public Dictionary<string, GetJsonBuffDefinition>? BuffMap { get; init; }

    [JsonProperty("skillMap")]
    public Dictionary<string, GetJsonSkillDefinition>? SkillMap { get; init; }

    [JsonProperty("timeStartStd")]
    public string? TimeStartStd { get; init; }

    [JsonProperty("encounterStart")]
    public string? EncounterStart { get; init; }

    [JsonProperty("logStart")]
    public string? LogStart { get; init; }

    [JsonProperty("timeEndStd")]
    public string? TimeEndStd { get; init; }

    [JsonProperty("encounterEnd")]
    public string? EncounterEnd { get; init; }

    [JsonProperty("logEnd")]
    public string? LogEnd { get; init; }

    [JsonProperty("eiEncounterID")]
    public long? EiEncounterId { get; init; }

    [JsonProperty("encounterID")]
    public long? EncounterId { get; init; }

    [JsonProperty("eiLogID")]
    public long? EiLogId { get; init; }

    [JsonProperty("logID")]
    public long? LogId { get; init; }

    [JsonProperty("success")]
    public bool? Success { get; init; }

    [JsonProperty("detailedWvW")]
    public bool? DetailedWvW { get; init; }

    [JsonProperty("fightName")]
    public string? FightName { get; init; }

    [JsonProperty("logName")]
    public string? LogName { get; init; }

    [JsonProperty("name")]
    public string? Name { get; init; }

    [JsonProperty("isCM")]
    public bool? IsChallengeMode { get; init; }

    [JsonProperty("isLegendaryCM")]
    public bool? IsLegendaryChallengeMode { get; init; }
}

public sealed class GetJsonTarget
{
    [JsonProperty("hitboxWidth")]
    public int? HitboxWidth { get; init; }

    [JsonProperty("hbWidth")]
    public int? HbWidth { get; init; }

    [JsonProperty("percent")]
    public float? Percent { get; init; }

    [JsonProperty("finalHealth")]
    public long? FinalHealth { get; init; }

    [JsonProperty("hpLeft")]
    public long? HpLeft { get; init; }

    [JsonProperty("name")]
    public string? Name { get; init; }

    [JsonProperty("totalHealth")]
    public long? TotalHealth { get; init; }

    [JsonProperty("health")]
    public long? Health { get; init; }

    [JsonProperty("totalDamageDist")]
    public JArray? TotalDamageDist { get; init; }
}

public sealed class GetJsonPlayer
{
    [JsonProperty("group")]
    public long? Group { get; init; }

    [JsonProperty("account")]
    public string? Account { get; init; }

    [JsonProperty("acc")]
    public string? Acc { get; init; }

    [JsonProperty("profession")]
    public string? Profession { get; init; }

    [JsonProperty("notInSquad")]
    public bool? NotInSquad { get; init; }

    [JsonProperty("name")]
    public string? Name { get; init; }

    [JsonProperty("instanceID")]
    public long? InstanceId { get; init; }

    [JsonProperty("combatReplayData")]
    public JObject? CombatReplayData { get; init; }

    [JsonProperty("rotation")]
    public JArray? Rotation { get; init; }

    [JsonProperty("dpsAll")]
    public JArray? DpsAll { get; init; }

    [JsonProperty("dpsTargets")]
    public JArray? DpsTargets { get; init; }

    [JsonProperty("statsAll")]
    public JArray? StatsAll { get; init; }

    [JsonProperty("statsTargets")]
    public JArray? StatsTargets { get; init; }

    [JsonProperty("defenses")]
    public JArray? Defenses { get; init; }

    [JsonProperty("support")]
    public JArray? Support { get; init; }

    [JsonProperty("buffUptimesActive")]
    public JArray? BuffUptimesActive { get; init; }

    [JsonProperty("buffUptimes")]
    public JArray? BuffUptimes { get; init; }

    [JsonProperty("groupBuffsActive")]
    public JArray? GroupBuffsActive { get; init; }

    [JsonProperty("groupBuffs")]
    public JArray? GroupBuffs { get; init; }

    [JsonProperty("offGroupBuffsActive")]
    public JArray? OffGroupBuffsActive { get; init; }

    [JsonProperty("offGroupBuffs")]
    public JArray? OffGroupBuffs { get; init; }

    [JsonProperty("extHealingStats")]
    public JObject? ExtHealingStats { get; init; }

    [JsonProperty("extBarrierStats")]
    public JObject? ExtBarrierStats { get; init; }
}

public sealed class GetJsonPhase
{
    [JsonProperty("name")]
    public string? Name { get; init; }

    [JsonProperty("start")]
    public double? Start { get; init; }

    [JsonProperty("end")]
    public double? End { get; init; }

    [JsonProperty("targets")]
    public List<int>? Targets { get; init; }
}

public sealed class GetJsonMechanic
{
    [JsonProperty("name")]
    public string? Name { get; init; }

    [JsonProperty("mechanicsData")]
    public JArray? MechanicsData { get; init; }
}

public sealed class GetJsonBuffDefinition
{
    [JsonProperty("classification")]
    public string? Classification { get; init; }
}

public sealed class GetJsonSkillDefinition
{
    [JsonProperty("aa")]
    public bool? Aa { get; init; }

    [JsonProperty("autoAttack")]
    public bool? AutoAttack { get; init; }

    [JsonProperty("name")]
    public string? Name { get; init; }
}
