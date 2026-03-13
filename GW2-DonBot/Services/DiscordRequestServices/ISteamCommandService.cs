namespace DonBot.Services.DiscordRequestServices;

public interface ISteamCommandService
{
    public Task VerifySteamAccount(Discord.WebSocket.SocketSlashCommand command);
}
