using Discord.WebSocket;

namespace Services.DiscordRequestServices
{
    public interface IRaidService
    {
        public Task StartRaid(SocketSlashCommand command, DiscordSocketClient discordClient);

        public Task CloseRaid(SocketSlashCommand command, DiscordSocketClient discordClient);
    }
}
