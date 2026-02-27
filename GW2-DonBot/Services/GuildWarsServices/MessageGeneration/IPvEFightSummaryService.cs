using Discord;
using DonBot.Models.GuildWars2;

namespace DonBot.Services.GuildWarsServices.MessageGeneration;

public interface IPvEFightSummaryService
{
    Task<Embed> GenerateSimple(EliteInsightDataModel data, long guildId);
}
