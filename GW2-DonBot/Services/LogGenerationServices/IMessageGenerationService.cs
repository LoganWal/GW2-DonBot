using Discord;
using Discord.WebSocket;
using Models;
using Models.Entities;

namespace Services.LogGenerationServices
{
    public interface IMessageGenerationService
    {
        public Embed GenerateWvWFightSummary(EliteInsightDataModel data, bool advancedLog, Guild guild, DiscordSocketClient client);

        public Task<Embed> GenerateWvWPlayerReport(Guild guildConfiguration);

        public Embed GeneratePvEFightSummary(EliteInsightDataModel data, long guildId);

        public Task<Embed> GenerateWvWPlayerSummary(Guild gw2Guild);

        public Task<Embed> GenerateWvWActivePlayerSummary(Guild gw2Guild, string fightLogUrl);

        public Embed? GenerateRaidReport(FightsReport fightsReportId);
    }
}
