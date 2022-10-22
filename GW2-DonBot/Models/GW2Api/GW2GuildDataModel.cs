using Newtonsoft.Json;
using static Controller.Discord.DiscordCore;

namespace Models.GW2Api
{
    public class GW2GuildDataModel
    {
        [JsonProperty("level")]
        public long Level { get; set; }

        [JsonProperty("motd")]
        public string Motd { get; set; }

        [JsonProperty("influence")]
        public long Influence { get; set; }

        [JsonProperty("aetherium")]
        public long Aetherium { get; set; }

        [JsonProperty("resonance")]
        public long Resonance { get; set; }

        [JsonProperty("favor")]
        public long Favor { get; set; }

        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("tag")]
        public string Tag { get; set; }

        [JsonProperty("emblem")]
        public GW2GuildEmblemDataModel Emblem { get; set; }
    }

    public partial class GW2GuildEmblemDataModel
    {
        [JsonProperty("background")]
        public GW2GuildGroundDataModel Background { get; set; }

        [JsonProperty("foreground")]
        public GW2GuildGroundDataModel Foreground { get; set; }

        [JsonProperty("flags")]
        public string[] Flags { get; set; }
    }

    public partial class GW2GuildGroundDataModel
    {
        [JsonProperty("id")]
        public long Id { get; set; }

        [JsonProperty("colors")]
        public long[] Colors { get; set; }
    }
}
