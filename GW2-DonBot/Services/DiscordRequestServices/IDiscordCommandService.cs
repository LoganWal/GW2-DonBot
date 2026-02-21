using Discord.WebSocket;

namespace DonBot.Services.DiscordRequestServices;

public interface IDiscordCommandService
{
    public Task ConfigureServer(SocketSlashCommand command, DiscordSocketClient discordClient);
}