using Discord;
using DonBot.Models.Entities;

namespace DonBot.Services.GuildWarsServices.MessageGeneration;

public interface IWeeklyLeaderboardService
{
    Task<List<Embed>?> GenerateWvW(Guild guild);
    Task<Embed?> GeneratePvE(Guild guild);
    Task<Embed?> GetPlayerRanks(Guild guild, List<string> accountNames);
}
