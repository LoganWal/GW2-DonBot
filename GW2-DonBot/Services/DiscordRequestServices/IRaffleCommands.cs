using Discord.WebSocket;

namespace Services.DiscordRequestServices
{
    public interface IRaffleCommands
    {
        public Task CreateRaffleCommandExecuted(SocketSlashCommand command, DiscordSocketClient discordClient);
        public Task CreateEventRaffleCommandExecuted(SocketSlashCommand command, DiscordSocketClient discordClient);
        public Task RaffleCommandExecuted(SocketSlashCommand command, DiscordSocketClient discordClient);
        public Task EventRaffleCommandExecuted(SocketSlashCommand command, DiscordSocketClient discordClient);
        public Task CompleteRaffleCommandExecuted(SocketSlashCommand command, DiscordSocketClient discordClient);
        public Task CompleteEventRaffleCommandExecuted(SocketSlashCommand command, DiscordSocketClient discordClient);
        public Task ReopenRaffleCommandExecuted(SocketSlashCommand command, DiscordSocketClient discordClient);
        public Task ReopenEventRaffleCommandExecuted(SocketSlashCommand command, DiscordSocketClient discordClient);
    }
}