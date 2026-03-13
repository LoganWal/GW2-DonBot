using Discord.WebSocket;

namespace DonBot.Services.DiscordRequestServices;

public interface IGenericCommandsService
{
    public Task AddQuoteCommandExecuted(SocketSlashCommand command);
}
