using Newtonsoft.Json;

namespace DonBot.Models.Apis.DeadlockApi;

public class DeadlockRank
{
    [JsonProperty("account_id")]
    public int AccountId { get; init; }

    [JsonProperty("player_score")]
    public int PlayerScore { get; init; }

    [JsonProperty("leaderboard_rank")]
    public int LeaderboardRank { get; init; }
}