using Discord.WebSocket;
using DonBot.Services.DatabaseServices;

namespace DonBot.Services.DiscordRequestServices;

public sealed class DiscordCommandService(IEntityService entityService) : IDiscordCommandService
{
    public async Task ConfigureServer(SocketSlashCommand command, DiscordSocketClient discordClient)
    {
        if (command.GuildId == null)
        {
            await command.FollowupAsync("This command must be used within a Discord server.", ephemeral: true);
            return;
        }

        var guild = await entityService.Guild.GetFirstOrDefaultAsync(g => g.GuildId == (long)command.GuildId);
        if (guild == null)
        {
            await command.FollowupAsync("Cannot find server configuration. The bot may not be set up for this server.", ephemeral: true);
            return;
        }

        var subCommand = command.Data.Options.First();
        var option = subCommand.Options.First();

        switch (subCommand.Name)
        {
            case "log_drop_off_channel":
                if (option.Value is not SocketTextChannel logDropOffChannel)
                {
                    await command.FollowupAsync("Please provide a valid text channel.", ephemeral: true);
                    return;
                }
                guild.LogDropOffChannelId = (long)logDropOffChannel.Id;
                break;

            case "guild_member_role":
                if (option.Value is not SocketRole guildMemberRole)
                {
                    await command.FollowupAsync("Please provide a valid role.", ephemeral: true);
                    return;
                }
                guild.DiscordGuildMemberRoleId = (long)guildMemberRole.Id;
                break;

            case "secondary_member_role":
                if (option.Value is not SocketRole secondaryMemberRole)
                {
                    await command.FollowupAsync("Please provide a valid role.", ephemeral: true);
                    return;
                }
                guild.DiscordSecondaryMemberRoleId = (long)secondaryMemberRole.Id;
                break;

            case "verified_role":
                if (option.Value is not SocketRole verifiedRole)
                {
                    await command.FollowupAsync("Please provide a valid role.", ephemeral: true);
                    return;
                }
                guild.DiscordVerifiedRoleId = (long)verifiedRole.Id;
                break;

            case "gw2_guild_member_role_id":
                guild.Gw2GuildMemberRoleId = option.Value.ToString();
                break;

            case "gw2_secondary_member_role_ids":
                guild.Gw2SecondaryMemberRoleIds = option.Value.ToString();
                break;

            case "player_report_channel":
                if (option.Value is not SocketTextChannel playerReportChannel)
                {
                    await command.FollowupAsync("Please provide a valid text channel.", ephemeral: true);
                    return;
                }
                guild.PlayerReportChannelId = (long)playerReportChannel.Id;
                break;

            case "wvw_activity_report_channel":
                if (option.Value is not SocketTextChannel wvwActivityChannel)
                {
                    await command.FollowupAsync("Please provide a valid text channel.", ephemeral: true);
                    return;
                }
                guild.WvwPlayerActivityReportChannelId = (long)wvwActivityChannel.Id;
                break;

            case "announcement_channel":
                if (option.Value is not SocketTextChannel announcementChannel)
                {
                    await command.FollowupAsync("Please provide a valid text channel.", ephemeral: true);
                    return;
                }
                guild.AnnouncementChannelId = (long)announcementChannel.Id;
                break;

            case "log_report_channel":
                if (option.Value is not SocketTextChannel logReportChannel)
                {
                    await command.FollowupAsync("Please provide a valid text channel.", ephemeral: true);
                    return;
                }
                guild.LogReportChannelId = (long)logReportChannel.Id;
                break;

            case "advance_log_report_channel":
                if (option.Value is not SocketTextChannel advanceLogChannel)
                {
                    await command.FollowupAsync("Please provide a valid text channel.", ephemeral: true);
                    return;
                }
                guild.AdvanceLogReportChannelId = (long)advanceLogChannel.Id;
                break;

            case "stream_log_channel":
                if (option.Value is not SocketTextChannel streamLogChannel)
                {
                    await command.FollowupAsync("Please provide a valid text channel.", ephemeral: true);
                    return;
                }
                guild.StreamLogChannelId = (long)streamLogChannel.Id;
                break;

            case "raid_alert_enabled":
                guild.RaidAlertEnabled = (bool)option.Value;
                break;

            case "raid_alert_channel":
                if (option.Value is not SocketTextChannel raidAlertChannel)
                {
                    await command.FollowupAsync("Please provide a valid text channel.", ephemeral: true);
                    return;
                }
                guild.RaidAlertChannelId = (long)raidAlertChannel.Id;
                break;

            case "remove_spam_enabled":
                guild.RemoveSpamEnabled = (bool)option.Value;
                break;

            case "removed_message_channel":
                if (option.Value is not SocketTextChannel removedMessageChannel)
                {
                    await command.FollowupAsync("Please provide a valid text channel.", ephemeral: true);
                    return;
                }
                guild.RemovedMessageChannelId = (long)removedMessageChannel.Id;
                break;

            case "auto_submit_to_wingman":
                guild.AutoSubmitToWingman = (bool)option.Value;
                break;

            case "auto_aggregate_logs":
                guild.AutoAggregateLogs = (bool)option.Value;
                break;

            case "auto_reply_single_log":
                guild.AutoReplySingleLog = (bool)option.Value;
                break;

            default:
                await command.FollowupAsync("Unknown configuration option.", ephemeral: true);
                return;
        }

        await entityService.Guild.UpdateAsync(guild);
        await command.FollowupAsync($"Successfully updated `{subCommand.Name}`.", ephemeral: true);
    }
}