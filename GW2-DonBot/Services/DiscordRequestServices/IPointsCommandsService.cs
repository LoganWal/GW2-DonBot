using Discord.WebSocket;

namespace DonBot.Services.DiscordRequestServices
{
    public interface IPointsCommandsService
    {
        public Task PointsCommandExecuted(SocketSlashCommand command);

        public Task PointsCommandExecuted(SocketMessageComponent command);
    }
}