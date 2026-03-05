using Discord.WebSocket;

namespace DonBot.Services.DiscordRequestServices;

public interface ILeaderboardCommandsService
{
    Task MyRankCommandExecuted(SocketSlashCommand command);
}
