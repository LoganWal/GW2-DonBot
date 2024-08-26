using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace DonBotDayOff.Services;

public class WordleService(HttpClient httpClient, ILogger<WordleService> logger) : IWordleService
{
    public async Task<string> FetchWordleWord()
    {
        var date = DateTime.Now.ToString("yyyy-MM-dd");
        var url = $"https://www.nytimes.com/svc/wordle/v2/{date}.json";
        var wordleWord = string.Empty;

        try
        {
            var response = await httpClient.GetStringAsync(url);
            var wordleData = JsonSerializer.Deserialize<WordleData>(response);
            wordleWord = wordleData?.Solution.ToLower() ?? string.Empty;
            logger.LogInformation($"current wordle word: {wordleWord}");
        }
        catch (Exception ex)
        {
            logger.LogError($"Error fetching Wordle word: {ex.Message}");
        }

        return wordleWord;
    }
}