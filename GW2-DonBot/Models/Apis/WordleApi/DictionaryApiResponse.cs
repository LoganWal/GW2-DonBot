using Newtonsoft.Json;

namespace DonBot.Models.Apis.WordleApi;

public class DictionaryApiResponse
{
    [JsonProperty("word")]
    public string? Word { get; set; }

    [JsonProperty("meanings")]
    public Meaning[] Meanings { get; set; } = [];
}

public class Meaning
{
    [JsonProperty("partOfSpeech")]
    public string? PartOfSpeech { get; set; }

    [JsonProperty("definitions")]
    public Definition[] Definitions { get; set; } = [];
}

public class Definition
{
    [JsonProperty("definition")]
    public string? DefinitionText { get; set; }
}