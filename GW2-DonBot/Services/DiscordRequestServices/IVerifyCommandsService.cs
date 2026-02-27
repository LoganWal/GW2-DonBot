using Discord.WebSocket;

namespace DonBot.Services.DiscordRequestServices;

public interface IVerifyCommandsService
{
    public Task VerifyCommandExecuted(SocketSlashCommand command, DiscordSocketClient discordClient);
    public Task DeverifyCommandExecuted(SocketSlashCommand command, DiscordSocketClient discordClient);
}