using Discord.WebSocket;

namespace DonBot.Services.DiscordRequestServices
{
    public interface IRaidCommandService
    {
        public Task StartRaid(SocketSlashCommand command, DiscordSocketClient discordClient);

        public Task CloseRaid(SocketSlashCommand command, DiscordSocketClient discordClient);
    }
}
