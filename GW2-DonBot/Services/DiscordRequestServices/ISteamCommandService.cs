using Discord.WebSocket;

namespace Services.DiscordRequestServices
{
    public interface ISteamCommandService
    {
        public Task VerifySteamAccount(SocketSlashCommand command, DiscordSocketClient discordClient);
    }
}
