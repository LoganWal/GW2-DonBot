using Discord;
using Discord.WebSocket;
using Handlers.MessageGenerationHandlers;
using Models;
using Models.Entities;

namespace Services.LogGenerationServices
{
    public class MessageGenerationService: IMessageGenerationService
    {
        private readonly WvWFightSummaryHandler _wvwFightSummaryHandler;
        private readonly WvWPlayerReportHandler _wvwPlayerReportHandler;
        private readonly PvEFightSummaryHandler _pveFightSummaryHandler;
        private readonly WvWPlayerSummaryHandler _wvwPlayerSummaryHandler;
        private readonly PvERaidReportHandler _pveRaidReportHandler;

        public MessageGenerationService(
            WvWFightSummaryHandler wvwFightSummaryHandler,
            WvWPlayerReportHandler wvwPlayerReportHandler,
            PvEFightSummaryHandler pveFightSummaryHandler,
            WvWPlayerSummaryHandler wvwPlayerSummaryHandler,
            PvERaidReportHandler pveRaidReportHandler)
        {
            _wvwFightSummaryHandler = wvwFightSummaryHandler;
            _wvwPlayerReportHandler = wvwPlayerReportHandler;
            _pveFightSummaryHandler = pveFightSummaryHandler;
            _wvwPlayerSummaryHandler = wvwPlayerSummaryHandler;
            _pveRaidReportHandler = pveRaidReportHandler;
        }

        public Embed GenerateWvWFightSummary(EliteInsightDataModel data, bool advancedLog, Guild guild, DiscordSocketClient client)
        {
            return _wvwFightSummaryHandler.Generate(data, advancedLog, guild, client);
        }

        public async Task<Embed> GenerateWvWPlayerReport(Guild guildConfiguration)
        {
            return await _wvwPlayerReportHandler.Generate(guildConfiguration);
        }

        public Embed GeneratePvEFightSummary(EliteInsightDataModel data, long guildId)
        {
            return _pveFightSummaryHandler.GenerateSimple(data, guildId);
        }

        public async Task<Embed> GenerateWvWPlayerSummary(Guild gw2Guild)
        {
            return await _wvwPlayerSummaryHandler.Generate(gw2Guild);
        }

        public async Task<Embed> GenerateWvWActivePlayerSummary(Guild gw2Guild, string fightLogUrl)
        {
            return await _wvwPlayerSummaryHandler.GenerateActive(gw2Guild, fightLogUrl);
        }

        public Embed? GenerateRaidReport(FightsReport fightsReport)
        {
            return _pveRaidReportHandler.Generate(fightsReport);
        }
    }
}