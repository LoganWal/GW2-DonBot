using Discord;
using DonBot.Models.Entities;
using DonBot.Models.GuildWars2;

namespace DonBot.Services.GuildWarsServices.MessageGeneration;

public interface IRaidReportService
{
    Task<List<Embed>?> Generate(FightsReport fightsReport, long guildId);
    Task<List<Embed>?> GenerateSimpleReply(List<string> urls, long guildId);
    Task<Embed> GenerateRaidAlert(long guildId);
}
