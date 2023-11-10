using Discord;
using Discord.WebSocket;
using Models;
using Models.Entities;

namespace Services.LogGenerationServices
{
    public interface IMessageGenerationService
    {
        public Task<Embed> GenerateWvWFightSummary(EliteInsightDataModel data, bool advancedLog, bool setPlayerPoint, Guild guild);

        public void GenerateWvWPlayerReport(Guild guild, SocketGuild discordGuild, string guildConfigurationWvwPlayerActivityReportWebhook);

        public Embed GeneratePvEFightSummary(EliteInsightDataModel data);

        public Task<Embed> GenerateWvWPlayerSummary(SocketGuild discordGuild, Guild gw2Guild);
    }
}
