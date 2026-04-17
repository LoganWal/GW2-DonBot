using Discord;
using Discord.WebSocket;
using DonBot.Models.Entities;
using DonBot.Models.GuildWars2;
using DonBot.Services.GuildWarsServices.MessageGeneration;

namespace DonBot.Services.GuildWarsServices;

public sealed class MessageGenerationService(
    IWvWFightSummaryService wvwFightSummaryService,
    IPvEFightSummaryService pveFightSummaryService,
    IRaidReportService raidReportService)
    : IMessageGenerationService
{
    public async Task<(Embed Embed, string? WebAppUrl)> GenerateWvWFightSummary(EliteInsightDataModel data, bool advancedLog, Guild guild, DiscordSocketClient client)
    {
        return await wvwFightSummaryService.Generate(data, advancedLog, guild, client);
    }

    public async Task<(Embed Embed, string? WebAppUrl)> GeneratePvEFightSummary(EliteInsightDataModel data, long guildId)
    {
        return await pveFightSummaryService.GenerateSimple(data, guildId);
    }

    public async Task<(List<Embed>? Embeds, string? WebAppUrl)> GenerateRaidReport(FightsReport fightsReport, long guildId)
    {
        return await raidReportService.Generate(fightsReport, guildId);
    }

    public async Task<(List<Embed>? Embeds, string? WebAppUrl)> GenerateRaidReplyReport(List<string> urls, long guildId)
    {
        return await raidReportService.GenerateSimpleReply(urls, guildId);
    }

    public async Task<Embed> GenerateRaidAlert(long guildId)
    {
        return await raidReportService.GenerateRaidAlert(guildId);
    }
}