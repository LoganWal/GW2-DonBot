using Discord;
using Discord.WebSocket;
using Models;
using Models.Entities;

namespace Services.DiscordMessagingServices
{
    public interface IMessageGenerationService
    {
        public Embed GenerateWvWFightSummary(EliteInsightDataModel data, bool advancedLog, bool setPlayerPoints);

        public Embed GeneratePvEFightSummary(EliteInsightDataModel data);

        public Embed GenerateWvWPlayerSummary(SocketGuild discordGuild, Guild gw2Guild);
    }
}
