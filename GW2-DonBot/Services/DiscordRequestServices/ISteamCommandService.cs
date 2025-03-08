using Discord.WebSocket;

namespace DonBot.Services.DiscordRequestServices
{
    public interface ISteamCommandService
    {
        public Task VerifySteamAccount(SocketSlashCommand command, DiscordSocketClient discordClient);
    }
}
