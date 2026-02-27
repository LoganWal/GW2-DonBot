using Newtonsoft.Json;

namespace DonBot.Models.Apis.WordleApi;

public class DictionaryApiResponse
{
    [JsonProperty("word")]
    public string? Word { get; init; }

    [JsonProperty("meanings")]
    public Meaning[] Meanings { get; init; } = [];
}

public class Meaning
{
    [JsonProperty("partOfSpeech")]
    public string? PartOfSpeech { get; init; }

    [JsonProperty("definitions")]
    public Definition[] Definitions { get; init; } = [];
}

public class Definition
{
    [JsonProperty("definition")]
    public string? DefinitionText { get; init; }
}