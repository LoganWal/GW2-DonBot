using Discord.WebSocket;

namespace Services.DiscordRequestServices
{
    public interface IPollingTasks
    {
        public Task PollingRoles(DiscordSocketClient discordClient);
    }
}