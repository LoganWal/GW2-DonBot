using Discord.WebSocket;
using DonBot.Models.Entities;
using DonBot.Models.Enums;
using DonBot.Services.DatabaseServices;

namespace DonBot.Services.DiscordRequestServices;

public sealed class DiscordCommandService(IEntityService entityService) : IDiscordCommandService
{
    public async Task ConfigureServer(SocketSlashCommand command)
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

            case "guild_member_role_id":
                guild.Gw2GuildMemberRoleId = option.Value.ToString();
                break;

            case "secondary_member_role_ids":
                guild.Gw2SecondaryMemberRoleIds = option.Value.ToString();
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

            case "wvw_leaderboard_enabled":
            {
                var enabled = (bool)option.Value;
                guild.WvwLeaderboardEnabled = enabled;
                var existing = await entityService.ScheduledEvent.GetFirstOrDefaultAsync(e =>
                    e.GuildId == guild.GuildId && e.EventType == (short)ScheduledEventTypeEnum.WvwLeaderboard);
                if (enabled)
                {
                    if (existing == null && guild.WvwLeaderboardChannelId.HasValue) {
                        await entityService.ScheduledEvent.AddAsync(BuildLeaderboardEvent(guild.GuildId, guild.WvwLeaderboardChannelId.Value, ScheduledEventTypeEnum.WvwLeaderboard));
                    }
                }
                else if (existing != null)
                {
                    await entityService.ScheduledEvent.DeleteAsync(existing);
                }
                break;
            }

            case "wvw_leaderboard_channel":
            {
                if (option.Value is not SocketTextChannel wvwLeaderboardChannel)
                {
                    await command.FollowupAsync("Please provide a valid text channel.", ephemeral: true);
                    return;
                }
                guild.WvwLeaderboardChannelId = (long)wvwLeaderboardChannel.Id;
                var existing = await entityService.ScheduledEvent.GetFirstOrDefaultAsync(e =>
                    e.GuildId == guild.GuildId && e.EventType == (short)ScheduledEventTypeEnum.WvwLeaderboard);
                if (existing != null) {
                    await entityService.ScheduledEvent.DeleteAsync(existing);
                }
                if (guild.WvwLeaderboardEnabled) {
                    await entityService.ScheduledEvent.AddAsync(BuildLeaderboardEvent(guild.GuildId, (long)wvwLeaderboardChannel.Id, ScheduledEventTypeEnum.WvwLeaderboard));
                }
                break;
            }

            case "pve_leaderboard_enabled":
            {
                var enabled = (bool)option.Value;
                guild.PveLeaderboardEnabled = enabled;
                var existing = await entityService.ScheduledEvent.GetFirstOrDefaultAsync(e =>
                    e.GuildId == guild.GuildId && e.EventType == (short)ScheduledEventTypeEnum.PveLeaderboard);
                if (enabled)
                {
                    if (existing == null && guild.PveLeaderboardChannelId.HasValue) {
                        await entityService.ScheduledEvent.AddAsync(BuildLeaderboardEvent(guild.GuildId, guild.PveLeaderboardChannelId.Value, ScheduledEventTypeEnum.PveLeaderboard));
                    }
                }
                else if (existing != null)
                {
                    await entityService.ScheduledEvent.DeleteAsync(existing);
                }
                break;
            }

            case "pve_leaderboard_channel":
            {
                if (option.Value is not SocketTextChannel pveLeaderboardChannel)
                {
                    await command.FollowupAsync("Please provide a valid text channel.", ephemeral: true);
                    return;
                }
                guild.PveLeaderboardChannelId = (long)pveLeaderboardChannel.Id;
                var existing = await entityService.ScheduledEvent.GetFirstOrDefaultAsync(e =>
                    e.GuildId == guild.GuildId && e.EventType == (short)ScheduledEventTypeEnum.PveLeaderboard);
                if (existing != null) {
                    await entityService.ScheduledEvent.DeleteAsync(existing);
                }
                if (guild.PveLeaderboardEnabled) {
                    await entityService.ScheduledEvent.AddAsync(BuildLeaderboardEvent(guild.GuildId, (long)pveLeaderboardChannel.Id, ScheduledEventTypeEnum.PveLeaderboard));
                }
                break;
            }

            default:
                await command.FollowupAsync("Unknown configuration option.", ephemeral: true);
                return;
        }

        await entityService.Guild.UpdateAsync(guild);
        await command.FollowupAsync($"Successfully updated `{subCommand.Name}`.", ephemeral: true);
    }

    private static ScheduledEvent BuildLeaderboardEvent(long guildId, long channelId, ScheduledEventTypeEnum eventType)
    {
        var now = DateTime.UtcNow;
        var daysUntilMonday = ((int)DayOfWeek.Monday - (int)now.DayOfWeek + 7) % 7;
        if (daysUntilMonday == 0) {
            daysUntilMonday = 7;
        }
        var nextMonday = new DateTime(now.Year, now.Month, now.Day, 0, 0, 0, DateTimeKind.Utc).AddDays(daysUntilMonday);

        return new ScheduledEvent
        {
            GuildId = guildId,
            ChannelId = channelId,
            EventType = (short)eventType,
            Day = (short)DayOfWeek.Monday,
            Hour = 0,
            RepeatIntervalDays = 7,
            UtcEventTime = nextMonday,
            Message = string.Empty
        };
    }
}