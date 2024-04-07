using Discord.WebSocket;

namespace Services.DiscordRequestServices
{
    public interface IDiscordCommandService
    {
        public Task SetLogChannel(SocketSlashCommand command, DiscordSocketClient discordClient);
    }
}
