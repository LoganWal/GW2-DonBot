using Discord.WebSocket;

namespace Services.DiscordRequestServices
{
    public interface IPointsCommandsService
    {
        public Task PointsCommandExecuted(SocketSlashCommand command);

        public Task PointsCommandExecuted(SocketMessageComponent command);
    }
}