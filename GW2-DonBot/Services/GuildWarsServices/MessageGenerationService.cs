using Discord;
using Discord.WebSocket;
using DonBot.Handlers.GuildWars2Handler.MessageGenerationHandlers;
using DonBot.Models.Entities;
using DonBot.Models.GuildWars2;

namespace DonBot.Services.GuildWarsServices
{
    public class MessageGenerationService(
        WvWFightSummaryHandler wvwFightSummaryHandler,
        WvWPlayerReportHandler wvwPlayerReportHandler,
        PvEFightSummaryHandler pveFightSummaryHandler,
        WvWPlayerSummaryHandler wvwPlayerSummaryHandler,
        RaidReportHandler raidReportHandler)
        : IMessageGenerationService
    {
        public Embed GenerateWvWFightSummary(EliteInsightDataModel data, bool advancedLog, Guild guild, DiscordSocketClient client)
        {
            return wvwFightSummaryHandler.Generate(data, advancedLog, guild, client);
        }

        public async Task<Embed> GenerateWvWPlayerReport(Guild guildConfiguration)
        {
            return await wvwPlayerReportHandler.Generate(guildConfiguration);
        }

        public Embed GeneratePvEFightSummary(EliteInsightDataModel data, long guildId)
        {
            return pveFightSummaryHandler.GenerateSimple(data, guildId);
        }

        public async Task<Embed> GenerateWvWPlayerSummary(Guild gw2Guild)
        {
            return await wvwPlayerSummaryHandler.Generate(gw2Guild);
        }

        public async Task<Embed> GenerateWvWActivePlayerSummary(Guild gw2Guild, string fightLogUrl)
        {
            return await wvwPlayerSummaryHandler.GenerateActive(gw2Guild, fightLogUrl);
        }

        public List<Embed>? GenerateRaidReport(FightsReport fightsReport, long guildId)
        {
            return raidReportHandler.Generate(fightsReport, guildId);
        }

        public Embed GenerateRaidAlert(long guildId)
        {
            return raidReportHandler.GenerateRaidAlert(guildId);
        }
    }
}