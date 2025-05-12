using Discord.WebSocket;
using DonBot.Services.DatabaseServices;

namespace DonBot.Services.DiscordRequestServices;

public class DiscordCommandService(IEntityService entityService) : IDiscordCommandService
{
    public async Task SetLogChannel(SocketSlashCommand command, DiscordSocketClient discordClient)
    {
        var channel = command.Data.Options.First().Value;
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

        var guild = await entityService.Guild.GetFirstOrDefaultAsync(g => g.GuildId == (long)command.GuildId);
        if (guild == null)
        {
            await command.FollowupAsync("Cannot find the related discord, try the command in the discord you want to change the log channel!", ephemeral: true);
            return;
        }

        guild.LogReportChannelId = channelId;

        await entityService.Guild.UpdateAsync(guild);
        await command.FollowupAsync("Update log channel!", ephemeral: true);
    }
}