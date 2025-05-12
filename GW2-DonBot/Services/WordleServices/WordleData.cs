using System.Text.Json.Serialization;

namespace DonBot.Services.WordleServices;

public class WordleData
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("solution")]
    public string Solution { get; set; } = string.Empty;

    [JsonPropertyName("print_date")]
    public string PrintDate { get; set; } = string.Empty;

    [JsonPropertyName("days_since_launch")]
    public int DaysSinceLaunch { get; set; }

    [JsonPropertyName("editor")]
    public string Editor { get; set; } = string.Empty;
}