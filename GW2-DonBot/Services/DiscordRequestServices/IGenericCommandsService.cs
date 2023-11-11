using Discord.WebSocket;

namespace Services.DiscordRequestServices
{
    public interface IGenericCommandsService
    {
        public Task HelpCommandExecuted(SocketSlashCommand command);
    }
}