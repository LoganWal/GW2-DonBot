using Discord;
using Discord.WebSocket;
using DonBot.Models.Entities;
using DonBot.Models.Enums;
using Microsoft.Extensions.Logging;

namespace DonBot.Services.SchedulerServices;

public sealed class WvwRaidSignupEventHandler(ILogger<WvwRaidSignupEventHandler> logger) : IScheduledEventHandler
{
    public ScheduledEventTypeEnum EventType => ScheduledEventTypeEnum.WvwRaidSignup;

    public async Task HandleAsync(ScheduledEvent scheduledEvent, SocketGuild socketGuild)
    {
        var channel = socketGuild.GetTextChannel((ulong)scheduledEvent.ChannelId);
        if (channel == null)
        {
            logger.LogWarning("Channel {ChannelId} not found in guild {GuildId}.", scheduledEvent.ChannelId, scheduledEvent.GuildId);
            return;
        }

        var localTime = scheduledEvent.UtcEventTime.ToLocalTime();
        var unixTimestamp = new DateTimeOffset(localTime).ToUnixTimeSeconds();

        var embed = new EmbedBuilder()
            .WithTitle("Event Roster")
            .WithDescription(string.Empty)
            .WithColor(Color.Blue)
            .AddField("✅ Roster", "No one has joined yet.")
            .AddField("❌ Can't Join", "No one has declined yet.")
            .AddField("⏰ Will Be Late", "No one will be late.");

        var component = new ComponentBuilder()
            .WithButton("Join", customId: $"join_{scheduledEvent.ScheduledEventId}", ButtonStyle.Success, Emoji.Parse("✅"))
            .WithButton("Can't Join", customId: $"cantjoin_{scheduledEvent.ScheduledEventId}", ButtonStyle.Secondary, Emoji.Parse("❌"))
            .WithButton("Will Be Late", customId: $"willlate_{scheduledEvent.ScheduledEventId}", ButtonStyle.Primary, Emoji.Parse("⏰"))
            .Build();

        var message = await channel.SendMessageAsync(
            text: $"<t:{unixTimestamp}:f>\n{scheduledEvent.Message}",
            embed: embed.Build(),
            components: component);

        scheduledEvent.MessageId = (long)message.Id;

        logger.LogInformation("Posted WvW raid signup to channel {ChannelId} in guild {GuildId}.", scheduledEvent.ChannelId, scheduledEvent.GuildId);
    }
}
