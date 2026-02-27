using Discord.WebSocket;

namespace DonBot.Services.DiscordRequestServices;

public interface IDeadlockCommandService
{
    public Task GetMmr(SocketSlashCommand command, DiscordSocketClient discordClient);

    public Task GetMmrHistory(SocketSlashCommand command, DiscordSocketClient discordClient);

    public Task GetMatchHistory(SocketSlashCommand command, DiscordSocketClient discordClient);
}