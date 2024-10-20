using DonBot.Controller.Discord;
using DonBot.Models.DeadlockApi;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace DonBot.Services.DeadlockServices
{
    internal class DeadlockApiService : IDeadlockApiService
    {
        private readonly HttpClient _httpClient;

        private readonly ILogger<DiscordCore> _logger;

        public DeadlockApiService(HttpClient httpClient, ILogger<DiscordCore> logger)
        {
            _httpClient = httpClient;
            _logger = logger;
        }


        public async Task<DeadlockRank> GetDeadlockRank(long accountId)
        {
            var requestUrl = $"https://analytics.deadlock-api.com/v1/players/{accountId}/rank";

            try
            {
                var response = await _httpClient.GetAsync(requestUrl);
                response.EnsureSuccessStatusCode();

                var jsonString = await response.Content.ReadAsStringAsync();
                var playerStats = JsonConvert.DeserializeObject<DeadlockRank>(jsonString) ?? new DeadlockRank();

                return playerStats;
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "Request error to GetDeadlockRank userId {discordId}", accountId);
                return new DeadlockRank();
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Json error to GetDeadlockRank userId {discordId}", accountId);
                return new DeadlockRank();
            }
        }

        public async Task<List<DeadlockRankHistory>> GetDeadlockRankHistory(long accountId)
        {
            var requestUrl = $"https://analytics.deadlock-api.com/v1/players/{accountId}/mmr-history";

            try
            {
                var response = await _httpClient.GetAsync(requestUrl);
                response.EnsureSuccessStatusCode();

                var jsonString = await response.Content.ReadAsStringAsync();
                var playerStats = JsonConvert.DeserializeObject<List<DeadlockRankHistory>>(jsonString) ?? new List<DeadlockRankHistory>();

                return playerStats;
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "Request error to GetDeadlockRankHistory userId {discordId}", accountId);
                return new List<DeadlockRankHistory>();
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Json error to GetDeadlockRankHistory userId {discordId}", accountId);
                return new List<DeadlockRankHistory>();
            }
        }
    }
}
