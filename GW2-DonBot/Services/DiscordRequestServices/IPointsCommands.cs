using Discord.WebSocket;

namespace Services.DiscordRequestServices
{
    public interface IPointsCommands
    {
        public Task PointsCommandExecuted(SocketSlashCommand command);
    }
}