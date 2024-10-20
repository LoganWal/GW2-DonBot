using Newtonsoft.Json;

namespace Models
{
    public class DictionaryApiResponse
    {
        [JsonProperty("word")]
        public string? Word { get; set; }

        [JsonProperty("meanings")]
        public Meaning[] Meanings { get; set; } = Array.Empty<Meaning>();
    }

    public class Meaning
    {
        [JsonProperty("partOfSpeech")]
        public string? PartOfSpeech { get; set; }

        [JsonProperty("definitions")]
        public Definition[] Definitions { get; set; } = Array.Empty<Definition>();
    }

    public class Definition
    {
        [JsonProperty("definition")]
        public string? DefinitionText { get; set; }
    }
}
