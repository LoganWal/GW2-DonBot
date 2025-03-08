using Discord.WebSocket;

namespace DonBot.Services.DiscordRequestServices
{
    public interface IPollingTasksService
    {
        public Task PollingRoles(DiscordSocketClient discordClient);
    }
}