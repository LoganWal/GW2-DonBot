using Discord;
using Discord.WebSocket;
using DonBot.Handlers.GuildWars2Handler.MessageGenerationHandlers;
using DonBot.Models.Entities;
using DonBot.Models.GuildWars2;

namespace DonBot.Services.GuildWarsServices;

public class MessageGenerationService(
    WvWFightSummaryHandler wvwFightSummaryHandler,
    WvWPlayerReportHandler wvwPlayerReportHandler,
    PvEFightSummaryHandler pveFightSummaryHandler,
    WvWPlayerSummaryHandler wvwPlayerSummaryHandler,
    RaidReportHandler raidReportHandler)
    : IMessageGenerationService
{
    public async Task<Embed> GenerateWvWFightSummary(EliteInsightDataModel data, bool advancedLog, Guild guild, DiscordSocketClient client)
    {
        return await wvwFightSummaryHandler.Generate(data, advancedLog, guild, client);
    }

    public async Task<Embed> GenerateWvWPlayerReport(Guild guildConfiguration)
    {
        return await wvwPlayerReportHandler.Generate(guildConfiguration);
    }

    public async Task<Embed> GeneratePvEFightSummary(EliteInsightDataModel data, long guildId)
    {
        return await pveFightSummaryHandler.GenerateSimple(data, guildId);
    }

    public async Task<Embed> GenerateWvWPlayerSummary(Guild gw2Guild)
    {
        return await wvwPlayerSummaryHandler.Generate(gw2Guild);
    }

    public async Task<Embed> GenerateWvWActivePlayerSummary(Guild gw2Guild, string fightLogUrl)
    {
        return await wvwPlayerSummaryHandler.GenerateActive(gw2Guild, fightLogUrl);
    }

    public async Task<List<Embed>?> GenerateRaidReport(FightsReport fightsReport, long guildId)
    {
        return await raidReportHandler.Generate(fightsReport, guildId);
    }

    public async Task<List<Embed>?> GenerateRaidReplyReport(List<string> urls)
    {
        return await raidReportHandler.GenerateSimpleReply(urls);
    }

    public async Task<Embed> GenerateRaidAlert(long guildId)
    {
        return await raidReportHandler.GenerateRaidAlert(guildId);
    }
}