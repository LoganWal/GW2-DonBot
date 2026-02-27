using Newtonsoft.Json;

namespace DonBot.Models.Apis.GuildWars2Api;

public class GuildWars2GuildDataModel
{
    [JsonProperty("level")]
    public long Level { get; init; }

    [JsonProperty("motd")]
    public string? Motd { get; init; }

    [JsonProperty("influence")]
    public long Influence { get; init; }

    [JsonProperty("aetherium")]
    public long Aetherium { get; init; }

    [JsonProperty("resonance")]
    public long Resonance { get; init; }

    [JsonProperty("favor")]
    public long Favor { get; init; }

    [JsonProperty("id")]
    public string? Id { get; init; }

    [JsonProperty("name")]
    public string? Name { get; init; }

    [JsonProperty("tag")]
    public string? Tag { get; init; }

    [JsonProperty("emblem")]
    public GuildWars2GuildEmblemDataModel? Emblem { get; init; }
}

public class GuildWars2GuildEmblemDataModel
{
    [JsonProperty("background")]
    public GuildWars2GuildGroundDataModel? Background { get; init; }

    [JsonProperty("foreground")]
    public GuildWars2GuildGroundDataModel? Foreground { get; init; }

    [JsonProperty("flags")]
    public string[]? Flags { get; init; }
}

public class GuildWars2GuildGroundDataModel
{
    [JsonProperty("id")]
    public long Id { get; init; }

    [JsonProperty("colors")]
    public long[]? Colors { get; init; }
}