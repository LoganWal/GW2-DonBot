namespace Models
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Converters;

    public class EliteInsightDataModel
    {
        [JsonProperty("url")]
        public string? Url { get; set; }

        [JsonProperty("targets")]
        public List<ArcDpsTarget>? Targets { get; set; }

        [JsonProperty("players")]
        public List<ArcDpsPlayer>? Players { get; set; }

        [JsonProperty("enemies")]
        public List<object>? Enemies { get; set; }

        [JsonProperty("phases")]
        public List<ArcDpsPhase>? Phases { get; set; }

        [JsonProperty("boons")]
        public List<long>? Boons { get; set; }

        [JsonProperty("offBuffs")]
        public List<long>? OffBuffs { get; set; }

        [JsonProperty("supBuffs")]
        public List<long>? SupBuffs { get; set; }

        [JsonProperty("defBuffs")]
        public List<long>? DefBuffs { get; set; }

        [JsonProperty("debuffs")]
        public List<long>? Debuffs { get; set; }

        [JsonProperty("gearBuffs")]
        public List<long>? GearBuffs { get; set; }

        [JsonProperty("nourishments")]
        public List<long>? Nourishments { get; set; }

        [JsonProperty("enhancements")]
        public List<long>? Enhancements { get; set; }

        [JsonProperty("otherConsumables")]
        public List<long>? OtherConsumables { get; set; }

        [JsonProperty("instanceBuffs")]
        public List<object>? InstanceBuffs { get; set; }

        [JsonProperty("dmgModifiersItem")]
        public List<long>? DmgModifiersItem { get; set; }

        [JsonProperty("dmgModifiersCommon")]
        public List<object>? DmgModifiersCommon { get; set; }

        [JsonProperty("dmgModifiersPers")]
        public Dictionary<string, List<long>>? DmgModifiersPers { get; set; }

        [JsonProperty("persBuffs")]
        public Dictionary<string, List<long>>? PersBuffs { get; set; }

        [JsonProperty("conditions")]
        public List<long>? Conditions { get; set; }

        [JsonProperty("skillMap")]
        public Dictionary<string, SkillMap>? SkillMap { get; set; }

        [JsonProperty("buffMap")]
        public Dictionary<string, BuffMap>? BuffMap { get; set; }

        [JsonProperty("damageModMap")]
        public Dictionary<string, DamageModMap>? DamageModMap { get; set; }

        [JsonProperty("mechanicMap")]
        public List<MechanicMap>? MechanicMap { get; set; }

        [JsonProperty("crData")]
        public CrData? CrData { get; set; }

        [JsonProperty("graphData")]
        public GraphData? GraphData { get; set; }

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

        [JsonProperty("hasCommander")]
        public bool HasCommander { get; set; }

        [JsonProperty("targetless")]
        public bool Targetless { get; set; }

        [JsonProperty("fightName")]
        public string? FightName { get; set; }

        [JsonProperty("fightIcon")]
        public string? FightIcon { get; set; }

        [JsonProperty("lightTheme")]
        public bool LightTheme { get; set; }

        [JsonProperty("noMechanics")]
        public bool NoMechanics { get; set; }

        [JsonProperty("singleGroup")]
        public bool SingleGroup { get; set; }

        [JsonProperty("hasBreakbarDamage")]
        public bool HasBreakbarDamage { get; set; }

        [JsonProperty("logErrors")]
        public List<string>? LogErrors { get; set; }

        [JsonProperty("encounterStart")]
        public string EncounterStart { get; set; } = string.Empty;

        [JsonProperty("encounterEnd")]
        public string EncounterEnd { get; set; } = string.Empty;

        [JsonProperty("arcVersion")]
        public string? ArcVersion { get; set; }

        [JsonProperty("evtcVersion")]
        public long EvtcVersion { get; set; }

        [JsonProperty("gw2Build")]
        public long Gw2Build { get; set; }

        [JsonProperty("triggerID")]
        public long TriggerId { get; set; }

        [JsonProperty("encounterID")]
        public long EncounterId { get; set; }

        [JsonProperty("parser")]
        public string? Parser { get; set; }

        [JsonProperty("recordedBy")]
        public string? RecordedBy { get; set; }

        [JsonProperty("uploadLinks")]
        public List<string>? UploadLinks { get; set; }

        [JsonProperty("usedExtensions")]
        public List<string>? UsedExtensions { get; set; }

        [JsonProperty("playersRunningExtensions")]
        public List<List<string>>? PlayersRunningExtensions { get; set; }
    }

    public class BarrierStatsExtension
    {
        [JsonProperty("barrierPhases")]
        public List<BarrierPhase>? BarrierPhases { get; set; }

        [JsonProperty("playerBarrierDetails")]
        public List<PlayerBarrierDetail>? PlayerBarrierDetails { get; set; }

        [JsonProperty("playerBarrierCharts")]
        public List<List<PlayerBarrierChart>>? PlayerBarrierCharts { get; set; }
    }

    public class BarrierPhase
    {
        [JsonProperty("outgoingBarrierStats")]
        public List<List<long>>? OutgoingBarrierStats { get; set; }

        [JsonProperty("outgoingBarrierStatsTargets")]
        public List<List<List<long>>>? OutgoingBarrierStatsTargets { get; set; }

        [JsonProperty("incomingBarrierStats")]
        public List<List<long>>? IncomingBarrierStats { get; set; }
    }

    public class PlayerBarrierChart
    {
        [JsonProperty("barrier")]
        public Barrier? Barrier { get; set; }
    }

    public class Barrier
    {
        [JsonProperty("targets")]
        public List<List<long>>? Targets { get; set; }

        [JsonProperty("total")]
        public List<double>? Total { get; set; }
    }

    public class PlayerBarrierDetail
    {
        [JsonProperty("barrierDistributions")]
        public List<BarrierDistribution>? BarrierDistributions { get; set; }

        [JsonProperty("barrierDistributionsTargets")]
        public List<List<BarrierDistribution>>? BarrierDistributionsTargets { get; set; }

        [JsonProperty("incomingBarrierDistributions")]
        public List<BarrierDistribution>? IncomingBarrierDistributions { get; set; }

        [JsonProperty("minions")]
        public List<PlayerBarrierDetailMinion>? Minions { get; set; }
    }

    public class BarrierDistribution
    {
        [JsonProperty("contributedBarrier")]
        public long ContributedBarrier { get; set; }

        [JsonProperty("totalBarrier")]
        public long TotalBarrier { get; set; }

        [JsonProperty("totalCasting")]
        public long TotalCasting { get; set; }

        [JsonProperty("distribution")]
        public List<List<Distribution>>? Distribution { get; set; }
    }

    public class PlayerBarrierDetailMinion
    {
        [JsonProperty("barrierDistributions")]
        public List<BarrierDistribution>? BarrierDistributions { get; set; }

        [JsonProperty("barrierDistributionsTargets")]
        public List<List<BarrierDistribution>>? BarrierDistributionsTargets { get; set; }
    }

    public class BuffMap
    {
        [JsonProperty("stacking")]
        public bool Stacking { get; set; }

        [JsonProperty("consumable")]
        public bool Consumable { get; set; }

        [JsonProperty("fightSpecific")]
        public bool FightSpecific { get; set; }

        [JsonProperty("id")]
        public long Id { get; set; }

        [JsonProperty("name")]
        public string? Name { get; set; }

        [JsonProperty("icon")]
        public string? Icon { get; set; }

        [JsonProperty("healingMode")]
        public long HealingMode { get; set; }

        [JsonProperty("description", NullValueHandling = NullValueHandling.Ignore)]
        public string? Description { get; set; }
    }

    public class CrData
    {
        [JsonProperty("actors")]
        public List<Actor>? Actors { get; set; }

        [JsonProperty("sizes")]
        public List<long>? Sizes { get; set; }

        [JsonProperty("maxTime")]
        public long MaxTime { get; set; }

        [JsonProperty("inchToPixel")]
        public double InchToPixel { get; set; }

        [JsonProperty("pollingRate")]
        public long PollingRate { get; set; }

        [JsonProperty("maps")]
        public List<Map>? Maps { get; set; }
    }

    public class Actor
    {
        [JsonProperty("group", NullValueHandling = NullValueHandling.Ignore)]
        public long? Group { get; set; }

        [JsonProperty("img", NullValueHandling = NullValueHandling.Ignore)]
        public string? Img { get; set; }

        [JsonProperty("type")]
        public string Type { get; set; }

        [JsonProperty("id", NullValueHandling = NullValueHandling.Ignore)]
        public long? Id { get; set; }

        [JsonProperty("positions", NullValueHandling = NullValueHandling.Ignore)]
        public List<double>? Positions { get; set; }

        [JsonProperty("dead", NullValueHandling = NullValueHandling.Ignore)]
        public List<double>? Dead { get; set; }

        [JsonProperty("down", NullValueHandling = NullValueHandling.Ignore)]
        public List<long>? Down { get; set; }

        [JsonProperty("dc", NullValueHandling = NullValueHandling.Ignore)]
        public List<double>? Dc { get; set; }

        [JsonProperty("start")]
        public long Start { get; set; }

        [JsonProperty("end")]
        public long End { get; set; }

        [JsonProperty("hitboxWidth", NullValueHandling = NullValueHandling.Ignore)]
        public long? HitboxWidth { get; set; }

        [JsonProperty("facingData", NullValueHandling = NullValueHandling.Ignore)]
        public List<double>? FacingData { get; set; }

        [JsonProperty("masterID", NullValueHandling = NullValueHandling.Ignore)]
        public long? MasterId { get; set; }

        [JsonProperty("fill", NullValueHandling = NullValueHandling.Ignore)]
        public bool? Fill { get; set; }

        [JsonProperty("growing", NullValueHandling = NullValueHandling.Ignore)]
        public long? Growing { get; set; }

        [JsonProperty("color", NullValueHandling = NullValueHandling.Ignore)]
        public string? Color { get; set; }
    }

    public class Map
    {
        [JsonProperty("link")]
        public string? Link { get; set; }

        [JsonProperty("start")]
        public long Start { get; set; }

        [JsonProperty("end")]
        public double End { get; set; }
    }

    public class DamageModMap
    {
        [JsonProperty("id")]
        public long Id { get; set; }

        [JsonProperty("name")]
        public string? Name { get; set; }

        [JsonProperty("icon")]
        public string? Icon { get; set; }

        [JsonProperty("tooltip")]
        public string? Tooltip { get; set; }

        [JsonProperty("nonMultiplier")]
        public bool NonMultiplier { get; set; }

        [JsonProperty("skillBased")]
        public bool SkillBased { get; set; }

        [JsonProperty("approximate")]
        public bool Approximate { get; set; }
    }

    public class GraphData
    {
        [JsonProperty("phases")]
        public List<GraphDataPhase>? Phases { get; set; }

        [JsonProperty("mechanics")]
        public List<Mechanic>? Mechanics { get; set; }
    }

    public class Mechanic
    {
        [JsonProperty("symbol")]
        public string? Symbol { get; set; }

        [JsonProperty("size")]
        public long Size { get; set; }

        [JsonProperty("color")]
        public string? Color { get; set; }

        [JsonProperty("visible")]
        public bool Visible { get; set; }
    }

    public class GraphDataPhase
    {
        [JsonProperty("players")]
        public List<PhasePlayer>? Players { get; set; }

        [JsonProperty("targets")]
        public List<PhaseTarget>? Targets { get; set; }

        [JsonProperty("targetsHealthStatesForCR")]
        public List<List<List<double>>>? TargetsHealthStatesForCr { get; set; }

        [JsonProperty("targetsBreakbarPercentStatesForCR")]
        public List<object>? TargetsBreakbarPercentStatesForCr { get; set; }

        [JsonProperty("targetsBarrierStatesForCR")]
        public List<object>? TargetsBarrierStatesForCr { get; set; }
    }

    public class PhasePlayer
    {
        [JsonProperty("damage")]
        public Barrier? Damage { get; set; }

        [JsonProperty("powerDamage")]
        public Barrier? PowerDamage { get; set; }

        [JsonProperty("conditionDamage")]
        public Barrier? ConditionDamage { get; set; }

        [JsonProperty("breakbarDamage")]
        public Barrier? BreakbarDamage { get; set; }

        [JsonProperty("healthStates")]
        public List<List<double>>? HealthStates { get; set; }

        [JsonProperty("barrierStates", NullValueHandling = NullValueHandling.Ignore)]
        public List<List<double>>? BarrierStates { get; set; }
    }

    public class PhaseTarget
    {
        [JsonProperty("total")]
        public List<long>? Total { get; set; }

        [JsonProperty("totalPower")]
        public List<long>? TotalPower { get; set; }

        [JsonProperty("totalCondition")]
        public List<long>? TotalCondition { get; set; }

        [JsonProperty("healthStates")]
        public List<List<double>>? HealthStates { get; set; }
    }

    public class HealingStatsExtension
    {
        [JsonProperty("healingPhases")]
        public List<HealingPhase>? HealingPhases { get; set; }

        [JsonProperty("playerHealingDetails")]
        public List<PlayerHealingDetail>? PlayerHealingDetails { get; set; }

        [JsonProperty("playerHealingCharts")]
        public List<List<PlayerHealingChart>>? PlayerHealingCharts { get; set; }
    }

    public class HealingPhase
    {
        [JsonProperty("outgoingHealingStats")]
        public List<List<long>>? OutgoingHealingStats { get; set; }

        [JsonProperty("outgoingHealingStatsTargets")]
        public List<List<List<long>>>? OutgoingHealingStatsTargets { get; set; }

        [JsonProperty("incomingHealingStats")]
        public List<List<long>>? IncomingHealingStats { get; set; }
    }

    public class PlayerHealingChart
    {
        [JsonProperty("healing")]
        public Barrier? Healing { get; set; }

        [JsonProperty("healingPowerHealing")]
        public Barrier? HealingPowerHealing { get; set; }

        [JsonProperty("conversionBasedHealing")]
        public Barrier? ConversionBasedHealing { get; set; }
    }

    public class PlayerHealingDetail
    {
        [JsonProperty("healingDistributions")]
        public List<HealingDistribution>? HealingDistributions { get; set; }

        [JsonProperty("healingDistributionsTargets")]
        public List<List<HealingDistribution>>? HealingDistributionsTargets { get; set; }

        [JsonProperty("incomingHealingDistributions")]
        public List<HealingDistribution>? IncomingHealingDistributions { get; set; }

        [JsonProperty("minions")]
        public List<PlayerHealingDetailMinion>? Minions { get; set; }
    }

    public class HealingDistribution
    {
        [JsonProperty("contributedHealing")]
        public long ContributedHealing { get; set; }

        [JsonProperty("contributedDownedHealing")]
        public long ContributedDownedHealing { get; set; }

        [JsonProperty("totalHealing")]
        public long TotalHealing { get; set; }

        [JsonProperty("totalCasting")]
        public long TotalCasting { get; set; }

        [JsonProperty("distribution")]
        public List<List<Distribution>>? Distribution { get; set; }
    }

    public class PlayerHealingDetailMinion
    {
        [JsonProperty("healingDistributions")]
        public List<HealingDistribution>? HealingDistributions { get; set; }

        [JsonProperty("healingDistributionsTargets")]
        public List<List<HealingDistribution>>? HealingDistributionsTargets { get; set; }
    }

    public class MechanicMap
    {
        [JsonProperty("name")]
        public string? Name { get; set; }

        [JsonProperty("icd")]
        public long Icd { get; set; }

        [JsonProperty("shortName")]
        public string? ShortName { get; set; }

        [JsonProperty("description")]
        public string? Description { get; set; }

        [JsonProperty("enemyMech")]
        public bool EnemyMech { get; set; }

        [JsonProperty("playerMech")]
        public bool PlayerMech { get; set; }
    }

    public class ArcDpsPhase
    {
        [JsonProperty("name")]
        public string? Name { get; set; }

        [JsonProperty("duration")]
        public long Duration { get; set; }

        [JsonProperty("start")]
        public long Start { get; set; }

        [JsonProperty("end")]
        public double End { get; set; }

        [JsonProperty("targets")]
        public List<long>? Targets { get; set; }

        [JsonProperty("breakbarPhase")]
        public bool BreakbarPhase { get; set; }

        [JsonProperty("dpsStats")]
        public List<List<double>>? DpsStats { get; set; }

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

        [JsonProperty("boonStats")]
        public List<BoonActiveStat>? BoonStats { get; set; }

        [JsonProperty("boonDictionaries")]
        public List<List<BoonActiveStat>>? BoonDictionaries { get; set; }

        [JsonProperty("boonGenSelfStats")]
        public List<BoonActiveStat>? BoonGenSelfStats { get; set; }

        [JsonProperty("boonGenGroupStats")]
        public List<BoonActiveStat>? BoonGenGroupStats { get; set; }

        [JsonProperty("boonGenOGroupStats")]
        public List<BoonActiveStat>? BoonGenOGroupStats { get; set; }

        [JsonProperty("boonGenSquadStats")]
        public List<BoonActiveStat>? BoonGenSquadStats { get; set; }

        [JsonProperty("offBuffStats")]
        public List<BoonActiveStat>? OffBuffStats { get; set; }

        [JsonProperty("offBuffGenSelfStats")]
        public List<BoonActiveStat>? OffBuffGenSelfStats { get; set; }

        [JsonProperty("offBuffGenGroupStats")]
        public List<BoonActiveStat>? OffBuffGenGroupStats { get; set; }

        [JsonProperty("offBuffGenOGroupStats")]
        public List<BoonActiveStat>? OffBuffGenOGroupStats { get; set; }

        [JsonProperty("offBuffGenSquadStats")]
        public List<BoonActiveStat>? OffBuffGenSquadStats { get; set; }

        [JsonProperty("supBuffStats")]
        public List<BoonActiveStat>? SupBuffStats { get; set; }

        [JsonProperty("supBuffGenSelfStats")]
        public List<BoonActiveStat>? SupBuffGenSelfStats { get; set; }

        [JsonProperty("supBuffGenGroupStats")]
        public List<BoonActiveStat>? SupBuffGenGroupStats { get; set; }

        [JsonProperty("supBuffGenOGroupStats")]
        public List<BoonActiveStat>? SupBuffGenOGroupStats { get; set; }

        [JsonProperty("supBuffGenSquadStats")]
        public List<BoonActiveStat>? SupBuffGenSquadStats { get; set; }

        [JsonProperty("defBuffStats")]
        public List<BoonActiveStat>? DefBuffStats { get; set; }

        [JsonProperty("defBuffGenSelfStats")]
        public List<BoonActiveStat>? DefBuffGenSelfStats { get; set; }

        [JsonProperty("defBuffGenGroupStats")]
        public List<BoonActiveStat>? DefBuffGenGroupStats { get; set; }

        [JsonProperty("defBuffGenOGroupStats")]
        public List<BoonActiveStat>? DefBuffGenOGroupStats { get; set; }

        [JsonProperty("defBuffGenSquadStats")]
        public List<BoonActiveStat>? DefBuffGenSquadStats { get; set; }

        [JsonProperty("conditionsStats")]
        public List<BoonActiveStat>? ConditionsStats { get; set; }

        [JsonProperty("persBuffStats")]
        public List<BoonActiveStat>? PersBuffStats { get; set; }

        [JsonProperty("gearBuffStats")]
        public List<BoonActiveStat>? GearBuffStats { get; set; }

        [JsonProperty("nourishmentStats")]
        public List<BoonActiveStat>? NourishmentStats { get; set; }

        [JsonProperty("enhancementStats")]
        public List<BoonActiveStat>? EnhancementStats { get; set; }

        [JsonProperty("otherConsumableStats")]
        public List<BoonActiveStat>? OtherConsumableStats { get; set; }

        [JsonProperty("debuffStats")]
        public List<BoonActiveStat>? DebuffStats { get; set; }

        [JsonProperty("boonActiveStats")]
        public List<BoonActiveStat>? BoonActiveStats { get; set; }

        [JsonProperty("boonActiveDictionaries")]
        public List<List<BoonActiveStat>>? BoonActiveDictionaries { get; set; }

        [JsonProperty("boonGenActiveSelfStats")]
        public List<BoonActiveStat>? BoonGenActiveSelfStats { get; set; }

        [JsonProperty("boonGenActiveGroupStats")]
        public List<BoonActiveStat>? BoonGenActiveGroupStats { get; set; }

        [JsonProperty("boonGenActiveOGroupStats")]
        public List<BoonActiveStat>? BoonGenActiveOGroupStats { get; set; }

        [JsonProperty("boonGenActiveSquadStats")]
        public List<BoonActiveStat>? BoonGenActiveSquadStats { get; set; }

        [JsonProperty("offBuffActiveStats")]
        public List<BoonActiveStat>? OffBuffActiveStats { get; set; }

        [JsonProperty("offBuffGenActiveSelfStats")]
        public List<BoonActiveStat>? OffBuffGenActiveSelfStats { get; set; }

        [JsonProperty("offBuffGenActiveGroupStats")]
        public List<BoonActiveStat>? OffBuffGenActiveGroupStats { get; set; }

        [JsonProperty("offBuffGenActiveOGroupStats")]
        public List<BoonActiveStat>? OffBuffGenActiveOGroupStats { get; set; }

        [JsonProperty("offBuffGenActiveSquadStats")]
        public List<BoonActiveStat>? OffBuffGenActiveSquadStats { get; set; }

        [JsonProperty("supBuffActiveStats")]
        public List<BoonActiveStat>? SupBuffActiveStats { get; set; }

        [JsonProperty("supBuffGenActiveSelfStats")]
        public List<BoonActiveStat>? SupBuffGenActiveSelfStats { get; set; }

        [JsonProperty("supBuffGenActiveGroupStats")]
        public List<BoonActiveStat>? SupBuffGenActiveGroupStats { get; set; }

        [JsonProperty("supBuffGenActiveOGroupStats")]
        public List<BoonActiveStat>? SupBuffGenActiveOGroupStats { get; set; }

        [JsonProperty("supBuffGenActiveSquadStats")]
        public List<BoonActiveStat>? SupBuffGenActiveSquadStats { get; set; }

        [JsonProperty("defBuffActiveStats")]
        public List<BoonActiveStat>? DefBuffActiveStats { get; set; }

        [JsonProperty("defBuffGenActiveSelfStats")]
        public List<BoonActiveStat>? DefBuffGenActiveSelfStats { get; set; }

        [JsonProperty("defBuffGenActiveGroupStats")]
        public List<BoonActiveStat>? DefBuffGenActiveGroupStats { get; set; }

        [JsonProperty("defBuffGenActiveOGroupStats")]
        public List<BoonActiveStat>? DefBuffGenActiveOGroupStats { get; set; }

        [JsonProperty("defBuffGenActiveSquadStats")]
        public List<BoonActiveStat>? DefBuffGenActiveSquadStats { get; set; }

        [JsonProperty("conditionsActiveStats")]
        public List<BoonActiveStat>? ConditionsActiveStats { get; set; }

        [JsonProperty("persBuffActiveStats")]
        public List<BoonActiveStat>? PersBuffActiveStats { get; set; }

        [JsonProperty("gearBuffActiveStats")]
        public List<BoonActiveStat>? GearBuffActiveStats { get; set; }

        [JsonProperty("debuffActiveStats")]
        public List<BoonActiveStat>? DebuffActiveStats { get; set; }

        [JsonProperty("dmgModifiersCommon")]
        public List<DmgModifiers>? DmgModifiersCommon { get; set; }

        [JsonProperty("dmgModifiersItem")]
        public List<DmgModifiers>? DmgModifiersItem { get; set; }

        [JsonProperty("dmgModifiersPers")]
        public List<DmgModifiers>? DmgModifiersPers { get; set; }

        [JsonProperty("targetsCondiStats")]
        public List<List<BoonActiveStat>>? TargetsCondiStats { get; set; }

        [JsonProperty("targetsCondiTotals")]
        public List<BoonActiveStat>? TargetsCondiTotals { get; set; }

        [JsonProperty("targetsBoonTotals")]
        public List<BoonActiveStat>? TargetsBoonTotals { get; set; }

        [JsonProperty("mechanicStats")]
        public List<List<object>>? MechanicStats { get; set; }

        [JsonProperty("enemyMechanicStats")]
        public List<object>? EnemyMechanicStats { get; set; }

        [JsonProperty("playerActiveTimes")]
        public List<long>? PlayerActiveTimes { get; set; }
    }

    public class BoonActiveStat
    {
        [JsonProperty("avg")]
        public double Avg { get; set; }

        [JsonProperty("data")]
        public List<List<double>>? Data { get; set; }
    }

    public class DmgModifiers
    {
        [JsonProperty("data")]
        public List<List<double>>? Data { get; set; }

        [JsonProperty("dataTarget")]
        public List<List<List<double>>>? DataTarget { get; set; }
    }

    public class ArcDpsPlayer
    {
        [JsonProperty("group")]
        public long Group { get; set; }

        [JsonProperty("acc")]
        public string? Acc { get; set; }

        [JsonProperty("profession")]
        public string? Profession { get; set; }

        [JsonProperty("isPoV")]
        public bool IsPoV { get; set; }

        [JsonProperty("isCommander")]
        public bool IsCommander { get; set; }

        [JsonProperty("l1Set")]
        public List<string>? L1Set { get; set; }

        [JsonProperty("l2Set")]
        public List<string>? L2Set { get; set; }

        [JsonProperty("a1Set")]
        public List<object>? A1Set { get; set; }

        [JsonProperty("a2Set")]
        public List<object>? A2Set { get; set; }

        [JsonProperty("colTarget")]
        public string? ColTarget { get; set; }

        [JsonProperty("colCleave")]
        public string? ColCleave { get; set; }

        [JsonProperty("colTotal")]
        public string? ColTotal { get; set; }

        [JsonProperty("isFake")]
        public bool IsFake { get; set; }

        [JsonProperty("notInSquad")]
        public bool NotInSquad { get; set; }

        [JsonProperty("uniqueID")]
        public long UniqueId { get; set; }

        [JsonProperty("name")]
        public string? Name { get; set; }

        [JsonProperty("tough")]
        public long Tough { get; set; }

        [JsonProperty("condi")]
        public long Condi { get; set; }

        [JsonProperty("conc")]
        public long Conc { get; set; }

        [JsonProperty("heal")]
        public long Heal { get; set; }

        [JsonProperty("icon")]
        public string? Icon { get; set; }

        [JsonProperty("health")]
        public long Health { get; set; }

        [JsonProperty("minions")]
        public List<PlayerMinion>? Minions { get; set; }

        [JsonProperty("details")]
        public PlayerDetails? Details { get; set; }
    }

    public class PlayerDetails
    {
        [JsonProperty("dmgDistributions")]
        public List<DmgDistribution>? DmgDistributions { get; set; }

        [JsonProperty("dmgDistributionsTargets")]
        public List<List<DmgDistribution>>? DmgDistributionsTargets { get; set; }

        [JsonProperty("dmgDistributionsTaken")]
        public List<DmgDistribution>? DmgDistributionsTaken { get; set; }

        [JsonProperty("rotation")]
        public List<List<List<double>>>? Rotation { get; set; }

        [JsonProperty("boonGraph")]
        public List<List<BoonGraph>>? BoonGraph { get; set; }

        [JsonProperty("food")]
        public List<Food>? Food { get; set; }

        [JsonProperty("minions")]
        public List<PurpleMinion>? Minions { get; set; }

        [JsonProperty("deathRecap", NullValueHandling = NullValueHandling.Ignore)]
        public List<DeathRecap>? DeathRecap { get; set; }
    }

    public class BoonGraph
    {
        [JsonProperty("id")]
        public long Id { get; set; }

        [JsonProperty("color")]
        public string? Color { get; set; }

        [JsonProperty("visible")]
        public bool Visible { get; set; }

        [JsonProperty("states")]
        public List<List<double>>? States { get; set; }
    }

    public class DeathRecap
    {
        [JsonProperty("time")]
        public long Time { get; set; }

        [JsonProperty("toDown")]
        public List<List<To>>? ToDown { get; set; }

        [JsonProperty("toKill", NullValueHandling = NullValueHandling.Ignore)]
        public List<List<To>>? ToKill { get; set; }
    }

    public class DmgDistribution
    {
        [JsonProperty("contributedDamage")]
        public long ContributedDamage { get; set; }

        [JsonProperty("contributedBreakbarDamage")]
        public double ContributedBreakbarDamage { get; set; }

        [JsonProperty("contributedShieldDamage")]
        public long ContributedShieldDamage { get; set; }

        [JsonProperty("totalDamage")]
        public long TotalDamage { get; set; }

        [JsonProperty("totalBreakbarDamage")]
        public double TotalBreakbarDamage { get; set; }

        [JsonProperty("totalCasting")]
        public long TotalCasting { get; set; }

        [JsonProperty("distribution", NullValueHandling = NullValueHandling.Ignore)]
        public List<List<Distribution>>? Distribution { get; set; }
    }

    public class Food
    {
        [JsonProperty("time")]
        public double Time { get; set; }

        [JsonProperty("duration")]
        public double Duration { get; set; }

        [JsonProperty("id")]
        public long Id { get; set; }

        [JsonProperty("stack")]
        public long Stack { get; set; }

        [JsonProperty("dimished")]
        public bool Dimished { get; set; }
    }

    public class PurpleMinion
    {
        [JsonProperty("dmgDistributions")]
        public List<DmgDistribution>? DmgDistributions { get; set; }

        [JsonProperty("dmgDistributionsTargets")]
        public List<List<DmgDistribution>>? DmgDistributionsTargets { get; set; }
    }

    public class PlayerMinion
    {
        [JsonProperty("id")]
        public long Id { get; set; }

        [JsonProperty("name")]
        public string? Name { get; set; }
    }

    public class SkillMap
    {
        [JsonProperty("aa")]
        public bool Aa { get; set; }

        [JsonProperty("isSwap")]
        public bool IsSwap { get; set; }

        [JsonProperty("notAccurate")]
        public bool NotAccurate { get; set; }

        [JsonProperty("id")]
        public long Id { get; set; }

        [JsonProperty("name")]
        public string? Name { get; set; }

        [JsonProperty("icon")]
        public string? Icon { get; set; }

        [JsonProperty("healingMode")]
        public long HealingMode { get; set; }
    }

    public class ArcDpsTarget
    {
        [JsonProperty("hbWidth")]
        public long HbWidth { get; set; }

        [JsonProperty("hbHeight")]
        public long HbHeight { get; set; }

        [JsonProperty("percent")]
        public long Percent { get; set; }

        [JsonProperty("hpLeft")]
        public long HpLeft { get; set; }

        [JsonProperty("uniqueID")]
        public long UniqueId { get; set; }

        [JsonProperty("name")]
        public string? Name { get; set; }

        [JsonProperty("tough")]
        public long Tough { get; set; }

        [JsonProperty("condi")]
        public long Condi { get; set; }

        [JsonProperty("conc")]
        public long Conc { get; set; }

        [JsonProperty("heal")]
        public long Heal { get; set; }

        [JsonProperty("icon")]
        public string? Icon { get; set; }

        [JsonProperty("health")]
        public long Health { get; set; }

        [JsonProperty("minions")]
        public List<PlayerMinion>? Minions { get; set; }

        [JsonProperty("details")]
        public TargetDetails? Details { get; set; }
    }

    public class TargetDetails
    {
        [JsonProperty("dmgDistributions")]
        public List<DmgDistribution>? DmgDistributions { get; set; }

        [JsonProperty("dmgDistributionsTaken")]
        public List<DmgDistribution>? DmgDistributionsTaken { get; set; }

        [JsonProperty("rotation")]
        public List<List<List<double>>>? Rotation { get; set; }

        [JsonProperty("boonGraph")]
        public List<List<BoonGraph>>? BoonGraph { get; set; }

        [JsonProperty("minions")]
        public List<FluffyMinion>? Minions { get; set; }
    }

    public class FluffyMinion
    {
        [JsonProperty("dmgDistributions")]
        public List<DmgDistribution>? DmgDistributions { get; set; }
    }

    public struct Distribution
    {
        public bool? Bool;
        public double? Double;

        public static implicit operator Distribution(bool Bool) => new Distribution { Bool = Bool };
        public static implicit operator Distribution(double Double) => new Distribution { Double = Double };
    }

    public struct DefStat
    {
        public double? Double;
        public string String;

        public static implicit operator DefStat(double Double) => new DefStat { Double = Double };
        public static implicit operator DefStat(string String) => new DefStat { String = String };
    }

    public struct To
    {
        public bool? Bool;
        public long? Integer;
        public string String;

        public static implicit operator To(bool Bool) => new To { Bool = Bool };
        public static implicit operator To(long Integer) => new To { Integer = Integer };
        public static implicit operator To(string String) => new To { String = String };
    }

    static class Converter
    {
        public static readonly JsonSerializerSettings Settings = new JsonSerializerSettings
        {
            MetadataPropertyHandling = MetadataPropertyHandling.Ignore,
            DateParseHandling = DateParseHandling.None,
            Converters =
            {
                DistributionConverter.Singleton,
                DefStatConverter.Singleton,
                ToConverter.Singleton,
                new IsoDateTimeConverter { DateTimeStyles = DateTimeStyles.AssumeUniversal }
            },
        };
    }

    class DistributionConverter : JsonConverter
    {
        public override bool CanConvert(Type t) => t == typeof(Distribution) || t == typeof(Distribution?);

        public override object ReadJson(JsonReader reader, Type t, object existingValue, JsonSerializer serializer)
        {
            switch (reader.TokenType)
            {
                case JsonToken.Integer:
                case JsonToken.Float:
                    var doubleValue = serializer.Deserialize<double>(reader);
                    return new Distribution { Double = doubleValue };
                case JsonToken.Boolean:
                    var boolValue = serializer.Deserialize<bool>(reader);
                    return new Distribution { Bool = boolValue };
            }
            throw new Exception("Cannot unmarshal type Distribution");
        }

        public override void WriteJson(JsonWriter writer, object untypedValue, JsonSerializer serializer)
        {
            var value = (Distribution)untypedValue;
            if (value.Double != null)
            {
                serializer.Serialize(writer, value.Double.Value);
                return;
            }
            if (value.Bool != null)
            {
                serializer.Serialize(writer, value.Bool.Value);
                return;
            }
            throw new Exception("Cannot marshal type Distribution");
        }

        public static readonly DistributionConverter Singleton = new DistributionConverter();
    }

    class DefStatConverter : JsonConverter
    {
        public override bool CanConvert(Type t) => t == typeof(DefStat) || t == typeof(DefStat?);

        public override object ReadJson(JsonReader reader, Type t, object existingValue, JsonSerializer serializer)
        {
            switch (reader.TokenType)
            {
                case JsonToken.Integer:
                case JsonToken.Float:
                    var doubleValue = serializer.Deserialize<double>(reader);
                    return new DefStat { Double = doubleValue };
                case JsonToken.String:
                case JsonToken.Date:
                    var stringValue = serializer.Deserialize<string>(reader);
                    return new DefStat { String = stringValue };
            }
            throw new Exception("Cannot unmarshal type DefStat");
        }

        public override void WriteJson(JsonWriter writer, object untypedValue, JsonSerializer serializer)
        {
            var value = (DefStat)untypedValue;
            if (value.Double != null)
            {
                serializer.Serialize(writer, value.Double.Value);
                return;
            }
            if (value.String != null)
            {
                serializer.Serialize(writer, value.String);
                return;
            }
            throw new Exception("Cannot marshal type DefStat");
        }

        public static readonly DefStatConverter Singleton = new DefStatConverter();
    }

    class ToConverter : JsonConverter
    {
        public override bool CanConvert(Type t) => t == typeof(To) || t == typeof(To?);

        public override object ReadJson(JsonReader reader, Type t, object existingValue, JsonSerializer serializer)
        {
            switch (reader.TokenType)
            {
                case JsonToken.Integer:
                    var integerValue = serializer.Deserialize<long>(reader);
                    return new To { Integer = integerValue };
                case JsonToken.Boolean:
                    var boolValue = serializer.Deserialize<bool>(reader);
                    return new To { Bool = boolValue };
                case JsonToken.String:
                case JsonToken.Date:
                    var stringValue = serializer.Deserialize<string>(reader);
                    return new To { String = stringValue };
            }
            throw new Exception("Cannot unmarshal type To");
        }

        public override void WriteJson(JsonWriter writer, object untypedValue, JsonSerializer serializer)
        {
            var value = (To)untypedValue;
            if (value.Integer != null)
            {
                serializer.Serialize(writer, value.Integer.Value);
                return;
            }
            if (value.Bool != null)
            {
                serializer.Serialize(writer, value.Bool.Value);
                return;
            }
            if (value.String != null)
            {
                serializer.Serialize(writer, value.String);
                return;
            }
            throw new Exception("Cannot marshal type To");
        }

        public static readonly ToConverter Singleton = new ToConverter();
    }
}
