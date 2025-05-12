using Discord.WebSocket;

namespace DonBot.Services.DiscordRequestServices;

public interface IDiscordCommandService
{
    public Task SetLogChannel(SocketSlashCommand command, DiscordSocketClient discordClient);
}