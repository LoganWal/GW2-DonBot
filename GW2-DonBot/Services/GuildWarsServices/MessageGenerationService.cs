using Discord;
using Discord.WebSocket;
using DonBot.Models.Entities;
using DonBot.Models.GuildWars2;
using DonBot.Services.GuildWarsServices.MessageGeneration;

namespace DonBot.Services.GuildWarsServices;

public sealed class MessageGenerationService(
    IWvWFightSummaryService wvwFightSummaryService,
    IWvWPlayerReportService wvwPlayerReportService,
    IPvEFightSummaryService pveFightSummaryService,
    IWvWPlayerSummaryService wvwPlayerSummaryService,
    IRaidReportService raidReportService)
    : IMessageGenerationService
{
    public async Task<Embed> GenerateWvWFightSummary(EliteInsightDataModel data, bool advancedLog, Guild guild, DiscordSocketClient client)
    {
        return await wvwFightSummaryService.Generate(data, advancedLog, guild, client);
    }

    public async Task<Embed> GenerateWvWPlayerReport(Guild guildConfiguration)
    {
        return await wvwPlayerReportService.Generate(guildConfiguration);
    }

    public async Task<Embed> GeneratePvEFightSummary(EliteInsightDataModel data, long guildId)
    {
        return await pveFightSummaryService.GenerateSimple(data, guildId);
    }

    public async Task<Embed> GenerateWvWPlayerSummary(Guild gw2Guild)
    {
        return await wvwPlayerSummaryService.Generate(gw2Guild);
    }

    public async Task<Embed> GenerateWvWActivePlayerSummary(Guild gw2Guild, string fightLogUrl)
    {
        return await wvwPlayerSummaryService.GenerateActive(gw2Guild, fightLogUrl);
    }

    public async Task<List<Embed>?> GenerateRaidReport(FightsReport fightsReport, long guildId)
    {
        return await raidReportService.Generate(fightsReport, guildId);
    }

    public async Task<List<Embed>?> GenerateRaidReplyReport(List<string> urls, long guildId)
    {
        return await raidReportService.GenerateSimpleReply(urls, guildId);
    }

    public async Task<Embed> GenerateRaidAlert(long guildId)
    {
        return await raidReportService.GenerateRaidAlert(guildId);
    }
}