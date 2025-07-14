using Newtonsoft.Json;

namespace DonBot.Models.GuildWars2;

public class HealingEliteInsightDataModel
{
    [JsonProperty("healingPhases")]
    public List<HealingPhase> HealingPhases = [];

    [JsonProperty("playerHealingDetails")]
    public List<PlayerHealingDetail> PlayerHealingDetails = [];

    [JsonProperty("playerHealingCharts")]
    public List<List<object>> PlayerHealingCharts = [];
}

public class HealingDistribution
{
    [JsonProperty("contributedHealing")]
    public int ContributedHealing;

    [JsonProperty("contributedDownedHealing")]
    public int ContributedDownedHealing;

    [JsonProperty("totalHealing")]
    public int TotalHealing;

    [JsonProperty("totalCasting")]
    public int TotalCasting;

    [JsonProperty("distribution")]
    public List<List<object>> Distribution = [];
}

public class HealingPhase
{
    [JsonProperty("outgoingHealingStats")]
    public List<List<int>> OutgoingHealingStats = [];

    [JsonProperty("outgoingHealingStatsTargets")]
    public List<List<List<int>>> OutgoingHealingStatsTargets = [];

    [JsonProperty("incomingHealingStats")]
    public List<List<int>> IncomingHealingStats = [];
}

public class IncomingHealingDistribution
{
    [JsonProperty("contributedHealing")]
    public int ContributedHealing;

    [JsonProperty("contributedDownedHealing")]
    public int ContributedDownedHealing;

    [JsonProperty("totalHealing")]
    public int TotalHealing;

    [JsonProperty("totalCasting")]
    public int TotalCasting;

    [JsonProperty("distribution")]
    public List<List<object>> Distribution = [];
}

public class HealingMinion
{
    [JsonProperty("healingDistributions")]
    public List<HealingDistribution> HealingDistributions = [];

    [JsonProperty("healingDistributionsTargets")]
    public List<List<object>> HealingDistributionsTargets = [];

    [JsonProperty("incomingHealingDistributions")]
    public List<IncomingHealingDistribution> IncomingHealingDistributions = [];

    [JsonProperty("minions")]
    public object? Minions;
}

public class PlayerHealingDetail
{
    [JsonProperty("healingDistributions")]
    public List<HealingDistribution> HealingDistributions = [];

    [JsonProperty("healingDistributionsTargets")]
    public List<List<object>> HealingDistributionsTargets = [];

    [JsonProperty("incomingHealingDistributions")]
    public List<IncomingHealingDistribution> IncomingHealingDistributions = [];

    [JsonProperty("minions")]
    public List<HealingMinion> Minions = [];
}