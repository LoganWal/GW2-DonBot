using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace DonBot.Services.WordleServices;

public class WordleService(
    ILogger<WordleService> logger, 
    IHttpClientFactory httpClientFactory)
    : IWordleService
{
    public async Task<string> FetchWordleWord()
    {
        var date = DateTime.Now.ToString("yyyy-MM-dd");
        var wordleWord = string.Empty;

        try
        {
            var response = await httpClientFactory.CreateClient().GetStringAsync($"https://www.nytimes.com/svc/wordle/v2/{date}.json");
            var wordleData = JsonSerializer.Deserialize<WordleData>(response);
            wordleWord = wordleData?.Solution.ToLower() ?? string.Empty;
            logger.LogInformation("current wordle word: {wordleWord}", wordleWord);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error fetching Wordle word");
        }

        return wordleWord;
    }
}