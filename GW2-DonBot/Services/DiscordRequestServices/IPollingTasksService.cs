using Discord.WebSocket;

namespace Services.DiscordRequestServices
{
    public interface IPollingTasksService
    {
        public Task PollingRoles(DiscordSocketClient discordClient);
    }
}