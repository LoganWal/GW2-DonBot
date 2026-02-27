using Newtonsoft.Json;

namespace DonBot.Models.GuildWars2;

public class SkillMapEntry
{
    [JsonProperty("id")]
    public long Id { get; set; }

    [JsonProperty("aa")]
    public bool IsAutoAttack { get; set; }

    [JsonProperty("name")]
    public string Name { get; set; } = string.Empty;
}
