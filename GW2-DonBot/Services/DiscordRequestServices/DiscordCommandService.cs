using Discord.WebSocket;
using DonBot.Models.Entities;

namespace DonBot.Services.DiscordRequestServices
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
            var channel = command.Data.Options.First().Value;
            // Parse the number of winners from the command options
            if (channel is not SocketTextChannel textChannel)
            {
                await command.FollowupAsync("Please try again and enter a valid text channel.", ephemeral: true);
                return;
            }

            var channelId = Convert.ToInt64(textChannel.Id);
            if (command.GuildId == null)
            {
                await command.FollowupAsync("Failed to update log channel, make sure to use this command within a discord server.", ephemeral: true);
                return;
            }

            var guild = _databaseContext.Guild.FirstOrDefault(g => g.GuildId == (long)command.GuildId);
            if (guild == null)
            {
                await command.FollowupAsync("Cannot find the related discord, try the command in the discord you want to change the log channel!", ephemeral: true);
                return;
            }

            guild.LogReportChannelId = channelId;

            _databaseContext.Update(guild);
            await _databaseContext.SaveChangesAsync();

            await command.FollowupAsync("Update log channel!", ephemeral: true);
        }
    }
}
