using Discord.WebSocket;

namespace Services.DiscordRequestServices
{
    public interface IRaffleCommandsService
    {
        public Task CreateRaffleCommandExecuted(SocketSlashCommand command, DiscordSocketClient discordClient);
        public Task CreateEventRaffleCommandExecuted(SocketSlashCommand command, DiscordSocketClient discordClient);
        public Task RaffleCommandExecuted(SocketSlashCommand command, DiscordSocketClient discordClient);
        public Task EventRaffleCommandExecuted(SocketSlashCommand command, DiscordSocketClient discordClient);
        public Task CompleteRaffleCommandExecuted(SocketSlashCommand command, DiscordSocketClient discordClient);
        public Task CompleteEventRaffleCommandExecuted(SocketSlashCommand command, DiscordSocketClient discordClient);
        public Task ReopenRaffleCommandExecuted(SocketSlashCommand command, DiscordSocketClient discordClient);
        public Task ReopenEventRaffleCommandExecuted(SocketSlashCommand command, DiscordSocketClient discordClient);

        public Task HandleRaffleButton1(SocketMessageComponent command);
        public Task HandleRaffleButton50(SocketMessageComponent command);
        public Task HandleRaffleButton100(SocketMessageComponent command);
        public Task HandleRaffleButton1000(SocketMessageComponent command);
        public Task HandleRaffleButtonRandom(SocketMessageComponent command);

        public Task HandleEventRaffleButton1(SocketMessageComponent command);
        public Task HandleEventRaffleButton50(SocketMessageComponent command);
        public Task HandleEventRaffleButton100(SocketMessageComponent command);
        public Task HandleEventRaffleButton1000(SocketMessageComponent command);
        public Task HandleEventRaffleButtonRandom(SocketMessageComponent command);
    }
}