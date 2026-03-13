namespace DonBot.Services.DiscordRequestServices;

public interface IDiscordCommandService
{
    public Task ConfigureServer(Discord.WebSocket.SocketSlashCommand command);
}
