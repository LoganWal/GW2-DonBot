using Newtonsoft.Json;

namespace DonBot.Models.Apis.DeadlockApi;

public class DeadlockRankHistory
{
    [JsonProperty("account_id")]
    public int AccountId { get; init; }

    [JsonProperty("match_id")]
    public int MatchId { get; init; }

    [JsonProperty("match_start_time")]
    public DateTime MatchStartTime { get; init; }

    [JsonProperty("player_score")]
    public int PlayerScore { get; init; }
}