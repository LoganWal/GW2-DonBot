using Discord;
using Discord.WebSocket;
using DonBot.Handlers.GuildWars2Handler.MessageGenerationHandlers;
using DonBot.Models.Entities;
using DonBot.Models.GuildWars2;

namespace DonBot.Services.GuildWarsServices
{
    public class MessageGenerationService: IMessageGenerationService
    {
        private readonly WvWFightSummaryHandler _wvwFightSummaryHandler;
        private readonly WvWPlayerReportHandler _wvwPlayerReportHandler;
        private readonly PvEFightSummaryHandler _pveFightSummaryHandler;
        private readonly WvWPlayerSummaryHandler _wvwPlayerSummaryHandler;
        private readonly RaidReportHandler _raidReportHandler;

        public MessageGenerationService(
            WvWFightSummaryHandler wvwFightSummaryHandler,
            WvWPlayerReportHandler wvwPlayerReportHandler,
            PvEFightSummaryHandler pveFightSummaryHandler,
            WvWPlayerSummaryHandler wvwPlayerSummaryHandler,
            RaidReportHandler raidReportHandler)
        {
            _wvwFightSummaryHandler = wvwFightSummaryHandler;
            _wvwPlayerReportHandler = wvwPlayerReportHandler;
            _pveFightSummaryHandler = pveFightSummaryHandler;
            _wvwPlayerSummaryHandler = wvwPlayerSummaryHandler;
            _raidReportHandler = raidReportHandler;
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

        public List<Embed>? GenerateRaidReport(FightsReport fightsReport, long guildId)
        {
            return _raidReportHandler.Generate(fightsReport, guildId);
        }

        public Embed GenerateRaidAlert(long guildId)
        {
            return _raidReportHandler.GenerateRaidAlert(guildId);
        }
    }
}