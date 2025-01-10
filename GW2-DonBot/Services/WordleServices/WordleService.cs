using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace DonBot.Services.WordleServices
{
    public class WordleService : IWordleService
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<WordleService> _logger;

        public WordleService(ILogger<WordleService> logger, IHttpClientFactory httpClientFactory)
        {
            _logger = logger;
            _httpClientFactory = httpClientFactory;
        }

        public async Task<string> FetchWordleWord()
        {
            var date = DateTime.Now.ToString("yyyy-MM-dd");
            var wordleWord = string.Empty;

            try
            {
                var response = await _httpClientFactory.CreateClient().GetStringAsync($"https://www.nytimes.com/svc/wordle/v2/{date}.json");
                var wordleData = JsonSerializer.Deserialize<WordleData>(response);
                wordleWord = wordleData?.Solution.ToLower() ?? string.Empty;
                _logger.LogInformation("current wordle word: {wordleWord}", wordleWord);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching Wordle word");
            }

            return wordleWord;
        }
    }
}