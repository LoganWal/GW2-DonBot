using Discord.WebSocket;

namespace Services.DiscordRequestServices
{
    public interface IGenericCommands
    {
        public Task HelpCommandExecuted(SocketSlashCommand command);
    }
}