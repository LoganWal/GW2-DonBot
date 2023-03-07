namespace Models
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using Extensions;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Converters;
    using Newtonsoft.Json.Linq;

    public partial class EliteInsightDataModel
    {
        [JsonProperty("url")]
        public string? Url{ get; set; }

        [JsonProperty("targets")]
        public EliteInsightDataModelTarget[]? Targets { get; set; }

        [JsonProperty("players")]
        public EliteInsightDataModelPlayer[]? Players { get; set; }

        [JsonProperty("enemies")]
        public object[]? Enemies { get; set; }

        [JsonProperty("phases")]
        public EliteInsightDataModelPhase[]? Phases { get; set; }

        [JsonProperty("boons")]
        public long[]? Boons { get; set; }

        [JsonProperty("offBuffs")]
        public long[]? OffBuffs { get; set; }

        [JsonProperty("supBuffs")]
        public long[]? SupBuffs { get; set; }

        [JsonProperty("defBuffs")]
        public long[]? DefBuffs { get; set; }

        [JsonProperty("debuffs")]
        public long[]? Debuffs { get; set; }

        [JsonProperty("gearBuffs")]
        public long[]? GearBuffs { get; set; }

        [JsonProperty("instanceBuffs")]
        public object[]? InstanceBuffs { get; set; }

        [JsonProperty("dmgModifiersItem")]
        public long[]? DmgModifiersItem { get; set; }

        [JsonProperty("dmgModifiersCommon")]
        public object[]? DmgModifiersCommon { get; set; }

        [JsonProperty("dmgModifiersPers")]
        public Dictionary<string, long[]>? DmgModifiersPers { get; set; }

        [JsonProperty("persBuffs")]
        public Dictionary<string, long[]>? PersBuffs { get; set; }

        [JsonProperty("conditions")]
        public long[]? Conditions { get; set; }

        [JsonProperty("skillMap")]
        public Dictionary<string, SkillMap>? SkillMap { get; set; }

        [JsonProperty("buffMap")]
        public Dictionary<string, BuffMap>? BuffMap { get; set; }

        [JsonProperty("damageModMap")]
        public Dictionary<string, DamageModMap>? DamageModMap { get; set; }

        [JsonProperty("mechanicMap")]
        public MechanicMap[]? MechanicMap { get; set; }

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
        public string[]? LogErrors { get; set; }

        [JsonProperty("encounterStart")]
        public string? EncounterStart { get; set; }

        [JsonProperty("encounterEnd")]
        public string? EncounterEnd { get; set; }

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
        public string[]? UploadLinks { get; set; }

        [JsonProperty("usedExtensions")]
        public string[]? UsedExtensions { get; set; }

        [JsonProperty("playersRunningExtensions")]
        public string[][]? PlayersRunningExtensions { get; set; }
    }

    public class BarrierStatsExtension
    {
        [JsonProperty("barrierPhases")]
        public BarrierPhase[]? BarrierPhases { get; set; }

        [JsonProperty("playerBarrierDetails")]
        public PlayerBarrierDetail[]? PlayerBarrierDetails { get; set; }

        [JsonProperty("playerBarrierCharts")]
        public PlayerBarrierChart[][]? PlayerBarrierCharts { get; set; }
    }

    public class BarrierPhase
    {
        [JsonProperty("outgoingBarrierStats")]
        public long[][]? OutgoingBarrierStats { get; set; }

        [JsonProperty("outgoingBarrierStatsTargets")]
        public long[][][]? OutgoingBarrierStatsTargets { get; set; }

        [JsonProperty("incomingBarrierStats")]
        public long[][]? IncomingBarrierStats { get; set; }
    }

    public class PlayerBarrierChart
    {
        [JsonProperty("barrier")]
        public Barrier? Barrier { get; set; }
    }

    public class Barrier
    {
        [JsonProperty("targets")]
        public double[][]? Targets { get; set; }

        [JsonProperty("total")]
        public double[]? Total { get; set; }
    }

    public class PlayerBarrierDetail
    {
        [JsonProperty("barrierDistributions")]
        public BarrierDistribution[]? BarrierDistributions { get; set; }

        [JsonProperty("barrierDistributionsTargets")]
        public BarrierDistribution[][]? BarrierDistributionsTargets { get; set; }

        [JsonProperty("incomingBarrierDistributions")]
        public BarrierDistribution[]? IncomingBarrierDistributions { get; set; }

        [JsonProperty("minions")]
        public PlayerBarrierDetailMinion[]? Minions { get; set; }
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
        public Distribution[][]? Distribution { get; set; }
    }

    public class PlayerBarrierDetailMinion
    {
        [JsonProperty("barrierDistributions")]
        public BarrierDistribution[]? BarrierDistributions { get; set; }

        [JsonProperty("barrierDistributionsTargets")]
        public BarrierDistribution[][]? BarrierDistributionsTargets { get; set; }
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
        public Actor[]? Actors { get; set; }

        [JsonProperty("sizes")]
        public long[]? Sizes { get; set; }

        [JsonProperty("maxTime")]
        public long MaxTime { get; set; }

        [JsonProperty("inchToPixel")]
        public double InchToPixel { get; set; }

        [JsonProperty("pollingRate")]
        public long PollingRate { get; set; }

        [JsonProperty("maps")]
        public Map[]? Maps { get; set; }
    }

    public class Actor
    {
        [JsonProperty("group", NullValueHandling = NullValueHandling.Ignore)]
        public long? Group { get; set; }

        [JsonProperty("img", NullValueHandling = NullValueHandling.Ignore)]
        public string? Img { get; set; }

        [JsonProperty("type")]
        public string? Type { get; set; }

        [JsonProperty("id", NullValueHandling = NullValueHandling.Ignore)]
        public long? Id { get; set; }

        [JsonProperty("positions", NullValueHandling = NullValueHandling.Ignore)]
        public double[]? Positions { get; set; }

        [JsonProperty("dead", NullValueHandling = NullValueHandling.Ignore)]
        public long[]? Dead { get; set; }

        [JsonProperty("down", NullValueHandling = NullValueHandling.Ignore)]
        public long[]? Down { get; set; }

        [JsonProperty("dc", NullValueHandling = NullValueHandling.Ignore)]
        public object[]? Dc { get; set; }

        [JsonProperty("start")]
        public long Start { get; set; }

        [JsonProperty("end")]
        public long End { get; set; }

        [JsonProperty("hitboxWidth", NullValueHandling = NullValueHandling.Ignore)]
        public long? HitboxWidth { get; set; }

        [JsonProperty("facingData", NullValueHandling = NullValueHandling.Ignore)]
        public double[]? FacingData { get; set; }

        [JsonProperty("connectedTo", NullValueHandling = NullValueHandling.Ignore), JsonConverter(typeof(SingleOrArrayConverter<long>))]
        public long[]? ConnectedTo { get; set; }

        [JsonProperty("masterID", NullValueHandling = NullValueHandling.Ignore)]
        public long? MasterId { get; set; }

        [JsonProperty("connectedFrom", NullValueHandling = NullValueHandling.Ignore)]
        public long? ConnectedFrom { get; set; }

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
        public long End { get; set; }
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
        public GraphDataPhase[]? Phases { get; set; }

        [JsonProperty("mechanics")]
        public Mechanic[]? Mechanics { get; set; }
    }

    public class Mechanic
    {
        [JsonProperty("symbol")]
        public string Symbol { get; set; }

        [JsonProperty("size")]
        public int Size { get; set; }

        [JsonProperty("color")]
        public string Color { get; set; }

        [JsonProperty("points")]
        public List<List<List<object>>> Points { get; set; }

        [JsonProperty("visible")]
        public bool Visible { get; set; }
    }

    public class GraphDataPhase
    {
        [JsonProperty("players")]
        public PhasePlayer[]? Players { get; set; }

        [JsonProperty("targets")]
        public PhaseTarget[]? Targets { get; set; }

        [JsonProperty("targetsHealthStatesForCR", NullValueHandling = NullValueHandling.Ignore)]
        public double[][][]? TargetsHealthStatesForCr { get; set; }

        [JsonProperty("targetsBreakbarPercentStatesForCR", NullValueHandling = NullValueHandling.Ignore)]
        public object[]? TargetsBreakbarPercentStatesForCr { get; set; }

        [JsonProperty("targetsBarrierStatesForCR", NullValueHandling = NullValueHandling.Ignore)]
        public object[]? TargetsBarrierStatesForCr { get; set; }
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
        public double[][]? HealthStates { get; set; }

        [JsonProperty("barrierStates", NullValueHandling = NullValueHandling.Ignore)]
        public double[][]? BarrierStates { get; set; }
    }

    public class PhaseTarget
    {
        [JsonProperty("total")]
        public long[]? Total { get; set; }

        [JsonProperty("totalPower")]
        public long[]? TotalPower { get; set; }

        [JsonProperty("totalCondition")]
        public long[]? TotalCondition { get; set; }

        [JsonProperty("healthStates")]
        public double[][]? HealthStates { get; set; }
    }

    public class HealingStatsExtension
    {
        [JsonProperty("healingPhases")]
        public HealingPhase[]? HealingPhases { get; set; }

        [JsonProperty("playerHealingDetails")]
        public PlayerHealingDetail[]? PlayerHealingDetails { get; set; }

        [JsonProperty("playerHealingCharts")]
        public PlayerHealingChart[][]? PlayerHealingCharts { get; set; }
    }

    public class HealingPhase
    {
        [JsonProperty("outgoingHealingStats")]
        public long[][]? OutgoingHealingStats { get; set; }

        [JsonProperty("outgoingHealingStatsTargets")]
        public long[][][]? OutgoingHealingStatsTargets { get; set; }

        [JsonProperty("incomingHealingStats")]
        public long[][]? IncomingHealingStats { get; set; }
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
        public HealingDistribution[]? HealingDistributions { get; set; }

        [JsonProperty("healingDistributionsTargets")]
        public HealingDistribution[][]? HealingDistributionsTargets { get; set; }

        [JsonProperty("incomingHealingDistributions")]
        public HealingDistribution[]? IncomingHealingDistributions { get; set; }

        [JsonProperty("minions")]
        public PlayerHealingDetailMinion[]? Minions { get; set; }
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
        public Distribution[][]? Distribution { get; set; }
    }

    public class PlayerHealingDetailMinion
    {
        [JsonProperty("healingDistributions")]
        public HealingDistribution[]? HealingDistributions { get; set; }

        [JsonProperty("healingDistributionsTargets")]
        public HealingDistribution[][]? HealingDistributionsTargets { get; set; }
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

    public class EliteInsightDataModelPhase
    {
        [JsonProperty("name")]
        public string? Name { get; set; }

        [JsonProperty("duration")]
        public long Duration { get; set; }

        [JsonProperty("start")]
        public double Start { get; set; }

        [JsonProperty("end")]
        public double End { get; set; }

        [JsonProperty("targets")]
        public long[]? Targets { get; set; }

        [JsonProperty("breakbarPhase")]
        public bool BreakbarPhase { get; set; }

        [JsonProperty("dummy")]
        public bool Dummy { get; set; }

        [JsonProperty("dpsStats")]
        public double[][]? DpsStats { get; set; }

        [JsonProperty("dpsStatsTargets")]
        public double[][][]? DpsStatsTargets { get; set; }

        [JsonProperty("offensiveStatsTargets")]
        public long[][][]? OffensiveStatsTargets { get; set; }

        [JsonProperty("offensiveStats")]
        public long[][]? OffensiveStats { get; set; }

        [JsonProperty("gameplayStats")]
        public double[][]? GameplayStats { get; set; }

        [JsonProperty("defStats")]
        public object[][]? DefStats { get; set; }

        [JsonProperty("supportStats")]
        public double[][]? SupportStats { get; set; }

        [JsonProperty("boonStats")]
        public BoonActiveStat[]? BoonStats { get; set; }

        [JsonProperty("boonGenSelfStats")]
        public BoonActiveStat[]? BoonGenSelfStats { get; set; }

        [JsonProperty("boonGenGroupStats")]
        public BoonActiveStat[]? BoonGenGroupStats { get; set; }

        [JsonProperty("boonGenOGroupStats")]
        public BoonActiveStat[]? BoonGenOGroupStats { get; set; }

        [JsonProperty("boonGenSquadStats")]
        public BoonActiveStat[]? BoonGenSquadStats { get; set; }

        [JsonProperty("offBuffStats")]
        public BoonActiveStat[]? OffBuffStats { get; set; }

        [JsonProperty("offBuffGenSelfStats")]
        public BoonActiveStat[]? OffBuffGenSelfStats { get; set; }

        [JsonProperty("offBuffGenGroupStats")]
        public BoonActiveStat[]? OffBuffGenGroupStats { get; set; }

        [JsonProperty("offBuffGenOGroupStats")]
        public BoonActiveStat[]? OffBuffGenOGroupStats { get; set; }

        [JsonProperty("offBuffGenSquadStats")]
        public BoonActiveStat[]? OffBuffGenSquadStats { get; set; }

        [JsonProperty("supBuffStats")]
        public BoonActiveStat[]? SupBuffStats { get; set; }

        [JsonProperty("supBuffGenSelfStats")]
        public BoonActiveStat[]? SupBuffGenSelfStats { get; set; }

        [JsonProperty("supBuffGenGroupStats")]
        public BoonActiveStat[]? SupBuffGenGroupStats { get; set; }

        [JsonProperty("supBuffGenOGroupStats")]
        public BoonActiveStat[]? SupBuffGenOGroupStats { get; set; }

        [JsonProperty("supBuffGenSquadStats")]
        public BoonActiveStat[]? SupBuffGenSquadStats { get; set; }

        [JsonProperty("defBuffStats")]
        public BoonActiveStat[]? DefBuffStats { get; set; }

        [JsonProperty("defBuffGenSelfStats")]
        public BoonActiveStat[]? DefBuffGenSelfStats { get; set; }

        [JsonProperty("defBuffGenGroupStats")]
        public BoonActiveStat[]? DefBuffGenGroupStats { get; set; }

        [JsonProperty("defBuffGenOGroupStats")]
        public BoonActiveStat[]? DefBuffGenOGroupStats { get; set; }

        [JsonProperty("defBuffGenSquadStats")]
        public BoonActiveStat[]? DefBuffGenSquadStats { get; set; }

        [JsonProperty("conditionsStats")]
        public BoonActiveStat[]? ConditionsStats { get; set; }

        [JsonProperty("persBuffStats")]
        public BoonActiveStat[]? PersBuffStats { get; set; }

        [JsonProperty("gearBuffStats")]
        public BoonActiveStat[]? GearBuffStats { get; set; }

        [JsonProperty("debuffStats")]
        public BoonActiveStat[]? DebuffStats { get; set; }

        [JsonProperty("boonActiveStats")]
        public BoonActiveStat[]? BoonActiveStats { get; set; }

        [JsonProperty("boonGenActiveSelfStats")]
        public BoonActiveStat[]? BoonGenActiveSelfStats { get; set; }

        [JsonProperty("boonGenActiveGroupStats")]
        public BoonActiveStat[]? BoonGenActiveGroupStats { get; set; }

        [JsonProperty("boonGenActiveOGroupStats")]
        public BoonActiveStat[]? BoonGenActiveOGroupStats { get; set; }

        [JsonProperty("boonGenActiveSquadStats")]
        public BoonActiveStat[]? BoonGenActiveSquadStats { get; set; }

        [JsonProperty("offBuffActiveStats")]
        public BoonActiveStat[]? OffBuffActiveStats { get; set; }

        [JsonProperty("offBuffGenActiveSelfStats")]
        public BoonActiveStat[]? OffBuffGenActiveSelfStats { get; set; }

        [JsonProperty("offBuffGenActiveGroupStats")]
        public BoonActiveStat[]? OffBuffGenActiveGroupStats { get; set; }

        [JsonProperty("offBuffGenActiveOGroupStats")]
        public BoonActiveStat[]? OffBuffGenActiveOGroupStats { get; set; }

        [JsonProperty("offBuffGenActiveSquadStats")]
        public BoonActiveStat[]? OffBuffGenActiveSquadStats { get; set; }

        [JsonProperty("supBuffActiveStats")]
        public BoonActiveStat[]? SupBuffActiveStats { get; set; }

        [JsonProperty("supBuffGenActiveSelfStats")]
        public BoonActiveStat[]? SupBuffGenActiveSelfStats { get; set; }

        [JsonProperty("supBuffGenActiveGroupStats")]
        public BoonActiveStat[]? SupBuffGenActiveGroupStats { get; set; }

        [JsonProperty("supBuffGenActiveOGroupStats")]
        public BoonActiveStat[]? SupBuffGenActiveOGroupStats { get; set; }

        [JsonProperty("supBuffGenActiveSquadStats")]
        public BoonActiveStat[]? SupBuffGenActiveSquadStats { get; set; }

        [JsonProperty("defBuffActiveStats")]
        public BoonActiveStat[]? DefBuffActiveStats { get; set; }

        [JsonProperty("defBuffGenActiveSelfStats")]
        public BoonActiveStat[]? DefBuffGenActiveSelfStats { get; set; }

        [JsonProperty("defBuffGenActiveGroupStats")]
        public BoonActiveStat[]? DefBuffGenActiveGroupStats { get; set; }

        [JsonProperty("defBuffGenActiveOGroupStats")]
        public BoonActiveStat[]? DefBuffGenActiveOGroupStats { get; set; }

        [JsonProperty("defBuffGenActiveSquadStats")]
        public BoonActiveStat[]? DefBuffGenActiveSquadStats { get; set; }

        [JsonProperty("conditionsActiveStats")]
        public BoonActiveStat[]? ConditionsActiveStats { get; set; }

        [JsonProperty("persBuffActiveStats")]
        public BoonActiveStat[]? PersBuffActiveStats { get; set; }

        [JsonProperty("gearBuffActiveStats")]
        public BoonActiveStat[]? GearBuffActiveStats { get; set; }

        [JsonProperty("debuffActiveStats")]
        public BoonActiveStat[]? DebuffActiveStats { get; set; }

        [JsonProperty("dmgModifiersCommon")]
        public DmgModifiers[]? DmgModifiersCommon { get; set; }

        [JsonProperty("dmgModifiersItem")]
        public DmgModifiers[]? DmgModifiersItem { get; set; }

        [JsonProperty("dmgModifiersPers")]
        public DmgModifiers[]? DmgModifiersPers { get; set; }

        [JsonProperty("targetsCondiStats")]
        public BoonActiveStat[][]? TargetsCondiStats { get; set; }

        [JsonProperty("targetsCondiTotals")]
        public BoonActiveStat[]? TargetsCondiTotals { get; set; }

        [JsonProperty("targetsBoonTotals")]
        public BoonActiveStat[]? TargetsBoonTotals { get; set; }

        [JsonProperty("mechanicStats")]
        public object[][]? MechanicStats { get; set; }

        [JsonProperty("enemyMechanicStats")]
        public object[]? EnemyMechanicStats { get; set; }

        [JsonProperty("playerActiveTimes")]
        public long[]? PlayerActiveTimes { get; set; }
    }

    public class BoonActiveStat
    {
        [JsonProperty("avg")]
        public double Avg { get; set; }

        [JsonProperty("data")]
        public double[][]? Data { get; set; }
    }

    public class DmgModifiers
    {
        [JsonProperty("data")]
        public double[][]? Data { get; set; }

        [JsonProperty("dataTarget")]
        public double[][][]? DataTarget { get; set; }
    }

    public class EliteInsightDataModelPlayer
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
        public string[]? L1Set { get; set; }

        [JsonProperty("l2Set")]
        public string[]? L2Set { get; set; }

        [JsonProperty("a1Set")]
        public object[]? A1Set { get; set; }

        [JsonProperty("a2Set")]
        public object[]? A2Set { get; set; }

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
        public PlayerMinion[]? Minions { get; set; }

        [JsonProperty("details")]
        public PlayerDetails? Details { get; set; }
    }

    public class PlayerDetails
    {
        [JsonProperty("dmgDistributions")]
        public DmgDistribution[]? DmgDistributions { get; set; }

        [JsonProperty("dmgDistributionsTargets")]
        public DmgDistribution[][]? DmgDistributionsTargets { get; set; }

        [JsonProperty("dmgDistributionsTaken")]
        public DmgDistribution[]? DmgDistributionsTaken { get; set; }

        [JsonProperty("rotation")]
        public double[][][]? Rotation { get; set; }

        [JsonProperty("boonGraph")]
        public BoonGraph[][]? BoonGraph { get; set; }

        [JsonProperty("food")]
        public Food[]? Food { get; set; }

        [JsonProperty("minions")]
        public PurpleMinion[]? Minions { get; set; }
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
        public double[][]? States { get; set; }
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
        public Distribution[][]? Distribution { get; set; }
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
        public DmgDistribution[]? DmgDistributions { get; set; }

        [JsonProperty("dmgDistributionsTargets")]
        public DmgDistribution[][]? DmgDistributionsTargets { get; set; }
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

    public class EliteInsightDataModelTarget
    {
        [JsonProperty("hbWidth")]
        public long HbWidth { get; set; }

        [JsonProperty("hbHeight")]
        public long HbHeight { get; set; }

        [JsonProperty("percent")]
        public double Percent { get; set; }

        [JsonProperty("hpLeft")]
        public double HpLeft { get; set; }

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
        public PlayerMinion[]? Minions { get; set; }

        [JsonProperty("details")]
        public TargetDetails? Details { get; set; }
    }

    public class TargetDetails
    {
        [JsonProperty("dmgDistributions")]
        public DmgDistribution[]? DmgDistributions { get; set; }

        [JsonProperty("dmgDistributionsTaken")]
        public DmgDistribution[]? DmgDistributionsTaken { get; set; }

        [JsonProperty("rotation")]
        public double[][][]? Rotation { get; set; }

        [JsonProperty("boonGraph")]
        public BoonGraph[][]? BoonGraph { get; set; }

        [JsonProperty("minions")]
        public FluffyMinion[]? Minions { get; set; }
    }

    public class FluffyMinion
    {
        [JsonProperty("dmgDistributions")]
        public DmgDistribution[]? DmgDistributions { get; set; }
    }

    public struct Distribution
    {
        public bool? HasDistribution;
        public double? DistributionValue;

        public static implicit operator Distribution(bool hasDistribution) => new() { HasDistribution = hasDistribution };
        public static implicit operator Distribution(double distributionValue) => new() { DistributionValue = distributionValue };
    }

    public struct DefStatElement
    {
        public string? DefStatName;
        public object? DefStatValue;

        public static implicit operator DefStatElement(string defStatName) => new() { DefStatName = defStatName };
        public static implicit operator DefStatElement(long defStatValue) => new() { DefStatValue = defStatValue };
    }

    public partial class EliteInsightDataModel
    {
        public static EliteInsightDataModel FromJson(string json) => JsonConvert.DeserializeObject<EliteInsightDataModel>(json, Converter.Settings) ?? new EliteInsightDataModel();
    }

    internal static class Converter
    {
        public static readonly JsonSerializerSettings Settings = new()
        {
            MetadataPropertyHandling = MetadataPropertyHandling.Ignore,
            DateParseHandling = DateParseHandling.None,
            Converters =
            {
                DistributionConverter.Singleton,
                DefStatElementConverter.Singleton,
                new IsoDateTimeConverter { DateTimeStyles = DateTimeStyles.AssumeUniversal }
            },
        };
    }

    internal class DistributionConverter : JsonConverter
    {
        public override bool CanConvert(Type t) => t == typeof(Distribution) || t == typeof(Distribution?);

        public override object ReadJson(JsonReader reader, Type t, object? existingValue, JsonSerializer serializer)
        {
            switch (reader.TokenType)
            {
                case JsonToken.Integer:
                case JsonToken.Float:
                    var doubleValue = serializer.Deserialize<double>(reader);
                    return new Distribution { DistributionValue = doubleValue };
                case JsonToken.Boolean:
                    var boolValue = serializer.Deserialize<bool>(reader);
                    return new Distribution { HasDistribution = boolValue };
            }
            throw new Exception("Cannot marshal type Distribution");
        }

        public override void WriteJson(JsonWriter writer, object? untypedValue, JsonSerializer serializer)
        {
            var value = untypedValue != null ? (Distribution)untypedValue : new Distribution();
            if (value.DistributionValue != null)
            {
                serializer.Serialize(writer, value.DistributionValue.Value);
                return;
            }

            if (value.HasDistribution == null)
            {
                throw new Exception("Cannot marshal type Distribution");
            }

            serializer.Serialize(writer, value.HasDistribution.Value);
        }

        public static readonly DistributionConverter Singleton = new();
    }

    internal class DefStatElementConverter : JsonConverter
    {
        public override bool CanConvert(Type t) => t == typeof(DefStatElement) || t == typeof(DefStatElement?);

        public override object ReadJson(JsonReader reader, Type t, object? existingValue, JsonSerializer serializer)
        {
            switch (reader.TokenType)
            {
                case JsonToken.Integer:
                    var integerValue = serializer.Deserialize<long>(reader);
                    return new DefStatElement { DefStatValue = integerValue };
                case JsonToken.String:
                case JsonToken.Date:
                    return new DefStatElement { DefStatName = serializer.Deserialize<string>(reader) };
            }
            throw new Exception("Cannot un-marshal type DefStatElement");
        }

        public override void WriteJson(JsonWriter writer, object? untypedValue, JsonSerializer serializer)
        {
            var value = untypedValue != null ? (DefStatElement)untypedValue : new DefStatElement();
            if (value.DefStatValue != null)
            {
                serializer.Serialize(writer, value.DefStatValue.TryParseLong());
                return;
            }
            if (value.DefStatName != null)
            {
                serializer.Serialize(writer, value.DefStatName);
            }
            throw new Exception("Cannot marshal type DefStatElement");
        }

        public static readonly DefStatElementConverter Singleton = new();
    }

    public class SingleOrArrayConverter<T> : JsonConverter
    {
        public override bool CanConvert(Type objectType)
        {
            return (objectType == typeof(T[]));
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            JToken token = JToken.Load(reader);
            if (token.Type == JTokenType.Array)
            {
                return token.ToObject<T[]>();
            }
            return new T[] { token.ToObject<T>() };
        }

        public override bool CanWrite
        {
            get { return false; }
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            throw new NotImplementedException();
        }
    }
}
