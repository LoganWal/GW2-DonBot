using Newtonsoft.Json;

namespace DonBot.Models.GuildWars2;

public class BarrierEliteInsightDataModel
{
    [JsonProperty("barrierPhases")]
    public List<BarrierPhase> BarrierPhases = [];

    [JsonProperty("playerBarrierDetails")]
    public List<PlayerBarrierDetail> PlayerBarrierDetails = [];

    [JsonProperty("playerBarrierCharts")]
    public List<List<object>> PlayerBarrierCharts = [];
}
public class BarrierDistribution
{
    [JsonProperty("contributedBarrier")]
    public int ContributedBarrier { get; set; }

    [JsonProperty("totalBarrier")]
    public int TotalBarrier { get; set; }

    [JsonProperty("totalCasting")]
    public int TotalCasting { get; set; }

    [JsonProperty("distribution")]
    public List<List<object>>? Distribution { get; set; }
}

public class BarrierPhase
{
    [JsonProperty("outgoingBarrierStats")]
    public List<List<int>> OutgoingBarrierStats = [];

    [JsonProperty("outgoingBarrierStatsTargets")]
    public List<List<List<int>>> OutgoingBarrierStatsTargets = [];

    [JsonProperty("incomingBarrierStats")]
    public List<List<int>> IncomingBarrierStats = [];
}

public class IncomingBarrierDistribution
{
    [JsonProperty("contributedBarrier")]
    public int ContributedBarrier;

    [JsonProperty("totalBarrier")]
    public int TotalBarrier;

    [JsonProperty("totalCasting")]
    public int TotalCasting;

    [JsonProperty("distribution")]
    public List<List<object>> Distribution = [];
}

public class BarrierMinion
{
    [JsonProperty("barrierDistributions")]
    public List<BarrierDistribution> BarrierDistributions = [];

    [JsonProperty("barrierDistributionsTargets")]
    public List<List<object>> BarrierDistributionsTargets = [];

    [JsonProperty("incomingBarrierDistributions")]
    public List<IncomingBarrierDistribution> IncomingBarrierDistributions = [];

    [JsonProperty("minions")]
    public object? Minions;
}

public class PlayerBarrierDetail
{
    [JsonProperty("barrierDistributions")]
    public List<BarrierDistribution> BarrierDistributions = [];

    [JsonProperty("barrierDistributionsTargets")]
    public List<List<object>> BarrierDistributionsTargets = [];

    [JsonProperty("incomingBarrierDistributions")]
    public List<IncomingBarrierDistribution> IncomingBarrierDistributions = [];

    [JsonProperty("minions")]
    public List<BarrierMinion> Minions = [];
}