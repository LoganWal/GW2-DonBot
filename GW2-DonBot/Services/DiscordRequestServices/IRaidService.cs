using Discord.WebSocket;

namespace Services.DiscordRequestServices
{
    public interface IRaidService
    {
        public Task StartRaid(SocketSlashCommand command);

        public Task CloseRaid(SocketSlashCommand command, DiscordSocketClient discordClient);
    }
}
