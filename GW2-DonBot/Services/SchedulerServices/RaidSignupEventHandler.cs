using Discord.WebSocket;
using DonBot.Core.Models.Entities;
using DonBot.Core.Models.Enums;
using Microsoft.Extensions.Logging;

namespace DonBot.Services.SchedulerServices;

public sealed class RaidSignupEventHandler(ILogger<RaidSignupEventHandler> logger) : IScheduledEventHandler
{
    public ScheduledEventTypeEnum EventType => ScheduledEventTypeEnum.RaidSignup;

    public async Task HandleAsync(ScheduledEvent scheduledEvent, SocketGuild socketGuild)
    {
        var channel = socketGuild.GetTextChannel((ulong)scheduledEvent.ChannelId);
        if (channel == null)
        {
            logger.LogWarning("Channel {ChannelId} not found in guild {GuildId}.", scheduledEvent.ChannelId, scheduledEvent.GuildId);
            return;
        }

        var message = await channel.SendMessageAsync(
            text: SignupMessageBuilder.BuildContent(scheduledEvent),
            embed: SignupMessageBuilder.BuildEmbed(scheduledEvent),
            components: SignupMessageBuilder.BuildComponents(scheduledEvent));

        scheduledEvent.MessageId = (long)message.Id;

        logger.LogInformation("Posted raid signup to channel {ChannelId} in guild {GuildId}.", scheduledEvent.ChannelId, scheduledEvent.GuildId);
    }
}
