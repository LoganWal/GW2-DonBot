using Discord;
using DonBot.Core.Models.GuildWars2;

namespace DonBot.Services.GuildWarsServices.MessageGeneration;

public interface IPvEFightSummaryService
{
    Task<(Embed Embed, string? WebAppUrl, long FightLogId)> GenerateSimple(EliteInsightDataModel data, long guildId);
}
