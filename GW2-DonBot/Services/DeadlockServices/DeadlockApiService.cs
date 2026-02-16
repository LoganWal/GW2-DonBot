using DonBot.Controller.Discord;
using DonBot.Models.Apis.DeadlockApi;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace DonBot.Services.DeadlockServices;

internal sealed class DeadlockApiService(ILogger<DiscordCore> logger, IHttpClientFactory httpClientFactory)
    : IDeadlockApiService
{
    public async Task<DeadlockRank> GetDeadlockRank(long accountId)
    {
        try
        {
            var response = await httpClientFactory.CreateClient().GetAsync($"https://analytics.deadlock-api.com/v1/players/{accountId}/rank");
            response.EnsureSuccessStatusCode();

            var jsonString = await response.Content.ReadAsStringAsync();
            var playerStats = JsonConvert.DeserializeObject<DeadlockRank>(jsonString) ?? new DeadlockRank();

            return playerStats;
        }
        catch (HttpRequestException ex)
        {
            logger.LogError(ex, "Request error to GetDeadlockRank userId {discordId}", accountId);
            return new DeadlockRank();
        }
        catch (JsonException ex)
        {
            logger.LogError(ex, "Json error to GetDeadlockRank userId {discordId}", accountId);
            return new DeadlockRank();
        }
    }

    public async Task<List<DeadlockRankHistory>> GetDeadlockRankHistory(long accountId)
    {
        try
        {
            var response = await httpClientFactory.CreateClient().GetAsync($"https://analytics.deadlock-api.com/v1/players/{accountId}/mmr-history");
            response.EnsureSuccessStatusCode();

            var jsonString = await response.Content.ReadAsStringAsync();
            var playerStats = JsonConvert.DeserializeObject<List<DeadlockRankHistory>>(jsonString) ?? [];

            return playerStats;
        }
        catch (HttpRequestException ex)
        {
            logger.LogError(ex, "Request error to GetDeadlockRankHistory userId {discordId}", accountId);
            return [];
        }
        catch (JsonException ex)
        {
            logger.LogError(ex, "Json error to GetDeadlockRankHistory userId {discordId}", accountId);
            return [];
        }
    }
}