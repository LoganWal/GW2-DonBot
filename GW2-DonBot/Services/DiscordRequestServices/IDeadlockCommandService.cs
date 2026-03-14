namespace DonBot.Services.DiscordRequestServices;

public interface IDeadlockCommandService
{
    public Task GetMmr(Discord.WebSocket.SocketSlashCommand command);

    public Task GetMmrHistory(Discord.WebSocket.SocketSlashCommand command);

    public Task GetMatchHistory(Discord.WebSocket.SocketSlashCommand command);
}
