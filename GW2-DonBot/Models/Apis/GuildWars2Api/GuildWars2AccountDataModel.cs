using Newtonsoft.Json;

namespace DonBot.Models.Apis.GuildWars2Api;

public class GuildWars2AccountDataModel
{
    [JsonProperty("id")]
    public Guid Id { get; set; }

    [JsonProperty("age")]
    public long Age { get; set; }

    [JsonProperty("name")]
    public string? Name { get; set; }

    [JsonProperty("world")]
    public long World { get; set; }

    [JsonProperty("commander")]
    public bool Commander { get; set; }

    [JsonProperty("guilds")]
    public string[] Guilds { get; set; } = [];

    [JsonProperty("access")]
    public string[] Access { get; set; } = [];

    [JsonProperty("created")]
    public DateTimeOffset Created { get; set; }

    [JsonProperty("guild_leader")]
    public string[] GuildLeader { get; set; } = [];

    [JsonProperty("fractal_level")]
    public long FractalLevel { get; set; }

    [JsonProperty("daily_ap")]
    public long DailyAp { get; set; }

    [JsonProperty("monthly_ap")]
    public long MonthlyAp { get; set; }

    [JsonProperty("wvw_rank")]
    public long WvwRank { get; set; }
}