using Discord.WebSocket;
using Models.Entities;

namespace Services.DiscordRequestServices
{
    public class DiscordCommandService : IDiscordCommandService
    {
        private readonly DatabaseContext _databaseContext;

        public DiscordCommandService(DatabaseContext databaseContext)
        {
            _databaseContext = databaseContext;
        }

        public async Task SetLogChannel(SocketSlashCommand command, DiscordSocketClient discordClient)
        {
            // Defer the command execution
            await command.DeferAsync(ephemeral: true);

            var channel = command.Data.Options.First().Value;
            // Parse the number of winners from the command options
            if (channel is not SocketTextChannel textChannel)
            {
                await command.ModifyOriginalResponseAsync(m => m.Content = "Please try again and enter a valid text channel.");
                return;
            }

            var channelId = Convert.ToInt64(textChannel.Id);
            if (command.GuildId == null)
            {
                await command.ModifyOriginalResponseAsync(m => m.Content = "Failed to update log channel, make sure to use this command within a discord server.");
                return;
            }

            var guild = _databaseContext.Guild.FirstOrDefault(g => g.GuildId == (long)command.GuildId);
            if (guild == null)
            {
                await command.ModifyOriginalResponseAsync(m => m.Content = "Cannot find the related discord, try the command in the discord you want to change the log channel!");
                return;
            }

            guild.LogReportChannelId = channelId;

            _databaseContext.Update(guild);
            _databaseContext.SaveChanges();

            await command.ModifyOriginalResponseAsync(m => m.Content = "Update log channel!"); ;
        }
    }
}
