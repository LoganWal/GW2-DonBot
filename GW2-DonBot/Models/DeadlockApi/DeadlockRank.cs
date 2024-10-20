using Newtonsoft.Json;

namespace DonBot.Models.DeadlockApi
{
    public class DeadlockRank
    {
        [JsonProperty("account_id")] 
        public int AccountId { get; set; }

        [JsonProperty("player_score")] 
        public int PlayerScore { get; set; }

        [JsonProperty("leaderboard_rank")] 
        public int LeaderboardRank { get; set; }
    }
}