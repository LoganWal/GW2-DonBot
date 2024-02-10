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

        public MessageGenerationService(
            WvWFightSummaryHandler wvwFightSummaryHandler,
            WvWPlayerReportHandler wvwPlayerReportHandler,
            PvEFightSummaryHandler pveFightSummaryHandler,
            WvWPlayerSummaryHandler wvwPlayerSummaryHandler)
        {
            _wvwFightSummaryHandler = wvwFightSummaryHandler;
            _wvwPlayerReportHandler = wvwPlayerReportHandler;
            _pveFightSummaryHandler = pveFightSummaryHandler;
            _wvwPlayerSummaryHandler = wvwPlayerSummaryHandler;
        }

        public Embed GenerateWvWFightSummary(EliteInsightDataModel data, bool advancedLog, Guild guild, DiscordSocketClient client)
        {
            return _wvwFightSummaryHandler.Generate(data, advancedLog, guild, client);
        }

        public async Task<Embed> GenerateWvWPlayerReport()
        {
            return await _wvwPlayerReportHandler.Generate();
        }

        public Embed GeneratePvEFightSummary(EliteInsightDataModel data)
        {
            return _pveFightSummaryHandler.Generate(data);
        }

        public async Task<Embed> GenerateWvWPlayerSummary(Guild gw2Guild)
        {
            return await _wvwPlayerSummaryHandler.Generate(gw2Guild);
        }
    }
}