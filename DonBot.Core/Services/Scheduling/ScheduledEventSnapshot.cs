using DonBot.Core.Models.Entities;
using DonBot.Core.Models.Scheduling;

namespace DonBot.Core.Services.Scheduling;

public sealed record ScheduledEventSnapshot(
    long ScheduledEventId,
    long GuildId,
    short EventType,
    long ChannelId,
    long? MessageId,
    DateTime? PostedEventTime,
    DateTime? LastNotificationEventTime,
    short Day,
    short Hour,
    DateTime UtcEventTime,
    short RepeatIntervalDays,
    short NotificationMinutesBeforeStart,
    string Message,
    string ResponseOptionsJson)
{
    public static ScheduledEventSnapshot Capture(ScheduledEvent scheduledEvent) =>
        new(
            scheduledEvent.ScheduledEventId,
            scheduledEvent.GuildId,
            scheduledEvent.EventType,
            scheduledEvent.ChannelId,
            scheduledEvent.MessageId,
            scheduledEvent.PostedEventTime,
            scheduledEvent.LastNotificationEventTime,
            scheduledEvent.Day,
            scheduledEvent.Hour,
            scheduledEvent.UtcEventTime,
            scheduledEvent.RepeatIntervalDays,
            scheduledEvent.NotificationMinutesBeforeStart,
            scheduledEvent.Message,
            scheduledEvent.ResponseOptionsJson);

    public bool IsPostedSignup =>
        ScheduledEventResponseOptions.IsSignupEvent(EventType) && MessageId.HasValue;

    public bool Matches(ScheduledEvent scheduledEvent) =>
        ScheduledEventId == scheduledEvent.ScheduledEventId
        && GuildId == scheduledEvent.GuildId
        && EventType == scheduledEvent.EventType
        && ChannelId == scheduledEvent.ChannelId
        && MessageId == scheduledEvent.MessageId
        && PostedEventTime == scheduledEvent.PostedEventTime
        && LastNotificationEventTime == scheduledEvent.LastNotificationEventTime
        && Day == scheduledEvent.Day
        && Hour == scheduledEvent.Hour
        && UtcEventTime == scheduledEvent.UtcEventTime
        && RepeatIntervalDays == scheduledEvent.RepeatIntervalDays
        && NotificationMinutesBeforeStart == scheduledEvent.NotificationMinutesBeforeStart
        && Message == scheduledEvent.Message
        && ResponseOptionsJson == scheduledEvent.ResponseOptionsJson;

    public void ApplyTo(ScheduledEvent scheduledEvent)
    {
        scheduledEvent.GuildId = GuildId;
        scheduledEvent.EventType = EventType;
        scheduledEvent.ChannelId = ChannelId;
        scheduledEvent.MessageId = MessageId;
        scheduledEvent.PostedEventTime = PostedEventTime;
        scheduledEvent.LastNotificationEventTime = LastNotificationEventTime;
        scheduledEvent.Day = Day;
        scheduledEvent.Hour = Hour;
        scheduledEvent.UtcEventTime = UtcEventTime;
        scheduledEvent.RepeatIntervalDays = RepeatIntervalDays;
        scheduledEvent.NotificationMinutesBeforeStart = NotificationMinutesBeforeStart;
        scheduledEvent.Message = Message;
        scheduledEvent.ResponseOptionsJson = ResponseOptionsJson;
    }
}
