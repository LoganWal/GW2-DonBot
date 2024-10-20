using Newtonsoft.Json;

namespace Models.DeadlockApi
{
    public class DeadlockRankHistory
    {
        [JsonProperty("account_id")]
        public int AccountId { get; set; }

        [JsonProperty("match_id")]
        public int MatchId { get; set; }

        [JsonProperty("match_start_time")]
        public DateTime MatchStartTime { get; set; }

        [JsonProperty("player_score")]
        public int PlayerScore { get; set; }
    }
}
