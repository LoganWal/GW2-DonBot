using Discord;
using Discord.WebSocket;
using DonBot.Models.Entities;
using DonBot.Models.Enums;
using DonBot.Services.DatabaseServices;
using DonBot.Services.GuildWarsServices.MessageGeneration;
using Microsoft.Extensions.Logging;

namespace DonBot.Services.SchedulerServices;

public sealed class PveLeaderboardEventHandler(
    IEntityService entityService,
    IWeeklyLeaderboardService weeklyLeaderboardService,
    ILogger<PveLeaderboardEventHandler> logger) : IScheduledEventHandler
{
    public ScheduledEventTypeEnum EventType => ScheduledEventTypeEnum.PveLeaderboard;

    public async Task HandleAsync(ScheduledEvent scheduledEvent, SocketGuild socketGuild)
    {
        var channel = socketGuild.GetTextChannel((ulong)scheduledEvent.ChannelId);
        if (channel == null)
        {
            logger.LogWarning("Channel {ChannelId} not found for PvE leaderboard.", scheduledEvent.ChannelId);
            return;
        }

        var guildEntity = await entityService.Guild.GetFirstOrDefaultAsync(g => g.GuildId == scheduledEvent.GuildId);
        if (guildEntity == null)
        {
            logger.LogWarning("Guild entity not found for {GuildId}.", scheduledEvent.GuildId);
            return;
        }

        var messages = await channel.GetMessagesAsync().FlattenAsync();
        var recentMessages = messages.Where(m => (DateTimeOffset.UtcNow - m.CreatedAt).TotalDays < 14).ToList();
        if (recentMessages.Count > 0) {
            await channel.DeleteMessagesAsync(recentMessages);
        }

        var embed = await weeklyLeaderboardService.GeneratePvE(guildEntity);
        if (embed != null) {
            await channel.SendMessageAsync(embeds: [embed]);
        }

        logger.LogInformation("Posted PvE leaderboard to channel {ChannelId} in guild {GuildId}.", scheduledEvent.ChannelId, scheduledEvent.GuildId);
    }
}
