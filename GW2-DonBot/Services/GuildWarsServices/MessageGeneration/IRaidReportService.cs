using Discord;
using DonBot.Models.Entities;

namespace DonBot.Services.GuildWarsServices.MessageGeneration;

public interface IRaidReportService
{
    Task<(List<Embed>? Embeds, string? WebAppUrl)> Generate(FightsReport fightsReport, long guildId);

    Task<(List<Embed>? Embeds, string? WebAppUrl)> GenerateSimpleReply(List<long> fightLogIds, long guildId);
    
    Task<Embed> GenerateRaidAlert(long guildId);
}
