using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Services
{
    public class WordleService : IWordleService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<WordleService> _logger;

        public WordleService(HttpClient httpClient, ILogger<WordleService> logger)
        {
            _httpClient = httpClient;
            _logger = logger;
        }

        public async Task<string> FetchWordleWord()
        {
            var date = DateTime.Now.ToString("yyyy-MM-dd");
            var url = $"https://www.nytimes.com/svc/wordle/v2/{date}.json";
            var wordleWord = string.Empty;

            try
            {
                var response = await _httpClient.GetStringAsync(url);
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