using Newtonsoft.Json;

namespace DonBot.Models.Apis.GuildWars2Api;

public class GuildWars2AccountDataModel
{
    [JsonProperty("id")]
    public Guid Id { get; init; }

    [JsonProperty("age")]
    public long Age { get; init; }

    [JsonProperty("name")]
    public string? Name { get; init; }

    [JsonProperty("world")]
    public long World { get; init; }

    [JsonProperty("commander")]
    public bool Commander { get; init; }

    [JsonProperty("guilds")]
    public string[] Guilds { get; init; } = [];

    [JsonProperty("access")]
    public string[] Access { get; init; } = [];

    [JsonProperty("created")]
    public DateTimeOffset Created { get; init; }

    [JsonProperty("guild_leader")]
    public string[] GuildLeader { get; init; } = [];

    [JsonProperty("fractal_level")]
    public long FractalLevel { get; init; }

    [JsonProperty("daily_ap")]
    public long DailyAp { get; init; }

    [JsonProperty("monthly_ap")]
    public long MonthlyAp { get; init; }

    [JsonProperty("wvw_rank")]
    public long WvwRank { get; init; }
}