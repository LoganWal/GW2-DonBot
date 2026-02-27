using System.Text.Json.Serialization;

namespace DonBot.Services.WordleServices;

public class WordleData
{
    [JsonPropertyName("id")]
    public int Id { get; init; }

    [JsonPropertyName("solution")]
    public string Solution { get; init; } = string.Empty;

    [JsonPropertyName("print_date")]
    public string PrintDate { get; init; } = string.Empty;

    [JsonPropertyName("days_since_launch")]
    public int DaysSinceLaunch { get; init; }

    [JsonPropertyName("editor")]
    public string Editor { get; init; } = string.Empty;
}