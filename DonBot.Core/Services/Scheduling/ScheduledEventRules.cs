using DonBot.Core.Models.Entities;
using DonBot.Core.Models.Enums;
using DonBot.Core.Models.Scheduling;

namespace DonBot.Core.Services.Scheduling;

public sealed record ScheduledEventWriteRequest(
    short EventType,
    long ChannelId,
    short Day,
    short Hour,
    DateTime UtcEventTime,
    short RepeatIntervalDays,
    string? Message,
    IReadOnlyList<ScheduledEventResponseOption>? ResponseOptions = null,
    short NotificationMinutesBeforeStart = ScheduledEventRules.DefaultNotificationMinutesBeforeStart);

public sealed record ScheduledEventTypeMetadata(
    short EventType,
    string Name,
    bool SupportsResponseOptions);

public sealed record ScheduledEventFormMetadata(
    int MaxMessageLength,
    short MinRepeatIntervalDays,
    short MaxRepeatIntervalDays,
    short DefaultNotificationMinutesBeforeStart,
    short MinNotificationMinutesBeforeStart,
    short MaxNotificationMinutesBeforeStart,
    int MaxResponseOptions,
    int MaxResponseOptionLabelLength,
    int MaxResponseOptionEmojiLength,
    IReadOnlyList<ScheduledEventTypeMetadata> EventTypes,
    IReadOnlyList<ScheduledEventResponseOption> DefaultSignupResponseOptions);

public static class ScheduledEventRules
{
    public const int MaxMessageLength = 256;
    public const short MinRepeatIntervalDays = 1;
    public const short MaxRepeatIntervalDays = 365;
    public const short DefaultNotificationMinutesBeforeStart = 15;
    public const short MinNotificationMinutesBeforeStart = 1;
    public const short MaxNotificationMinutesBeforeStart = 10080;

    public static ScheduledEventFormMetadata GetFormMetadata() =>
        new(
            MaxMessageLength,
            MinRepeatIntervalDays,
            MaxRepeatIntervalDays,
            DefaultNotificationMinutesBeforeStart,
            MinNotificationMinutesBeforeStart,
            MaxNotificationMinutesBeforeStart,
            ScheduledEventResponseOptions.MaxCount,
            ScheduledEventResponseOptions.MaxLabelLength,
            ScheduledEventResponseOptions.MaxEmojiLength,
            [
                new((short)ScheduledEventTypeEnum.RaidSignup, nameof(ScheduledEventTypeEnum.RaidSignup), true),
                new((short)ScheduledEventTypeEnum.WvwLeaderboard, nameof(ScheduledEventTypeEnum.WvwLeaderboard), false),
                new((short)ScheduledEventTypeEnum.PveLeaderboard, nameof(ScheduledEventTypeEnum.PveLeaderboard), false)
            ],
            ScheduledEventResponseOptions.DefaultsForEventType((short)ScheduledEventTypeEnum.RaidSignup));

    public static string? ValidateWriteRequest(
        ScheduledEventWriteRequest request,
        DateTime? nowUtc = null)
    {
        var scheduleError = ValidateScheduleFields(request, nowUtc);
        if (scheduleError is not null)
        {
            return scheduleError;
        }

        return ValidateContentFields(request);
    }

    public static string? ValidateScheduleFields(
        ScheduledEventWriteRequest request,
        DateTime? nowUtc = null)
    {
        if (!Enum.IsDefined(typeof(ScheduledEventTypeEnum), request.EventType))
        {
            return "Invalid event type.";
        }

        if (request.EventType == 3)
        {
            return "Invalid event type.";
        }

        if (request.EventType == (short)ScheduledEventTypeEnum.WvwRaidSignup)
        {
            return "WvW raid signup has been consolidated into raid signup. Use response options instead.";
        }

        if (request.Day < 0 || request.Day > 6)
        {
            return "Post day must be 0-6 (Sunday-Saturday).";
        }

        if (request.Hour < 0 || request.Hour > 23)
        {
            return "Post hour must be 0-23.";
        }

        if (request.UtcEventTime == default)
        {
            return "Event time is required.";
        }

        var comparisonNow = nowUtc ?? DateTime.UtcNow;
        if (request.UtcEventTime <= comparisonNow)
        {
            return "Event time must be in the future.";
        }

        if (request.RepeatIntervalDays < MinRepeatIntervalDays || request.RepeatIntervalDays > MaxRepeatIntervalDays)
        {
            return $"Repeat interval must be {MinRepeatIntervalDays}-{MaxRepeatIntervalDays} days.";
        }

        if (request.NotificationMinutesBeforeStart < MinNotificationMinutesBeforeStart
            || request.NotificationMinutesBeforeStart > MaxNotificationMinutesBeforeStart)
        {
            return $"Notification lead time must be {MinNotificationMinutesBeforeStart}-{MaxNotificationMinutesBeforeStart} minutes.";
        }

        return null;
    }

    public static string? ValidateContentFields(ScheduledEventWriteRequest request)
    {
        if (!string.IsNullOrEmpty(request.Message) && request.Message.Length > MaxMessageLength)
        {
            return $"Message must be {MaxMessageLength} characters or fewer.";
        }

        return ScheduledEventResponseOptions.ValidateForEventType(request.EventType, request.ResponseOptions);
    }

    public static bool ShouldCheckSignupNotification(ScheduledEvent scheduledEvent, DateTime now)
    {
        if (!ScheduledEventResponseOptions.IsSignupEvent(scheduledEvent.EventType)
            || scheduledEvent.NotificationMinutesBeforeStart < MinNotificationMinutesBeforeStart
            || !scheduledEvent.MessageId.HasValue
            || !scheduledEvent.PostedEventTime.HasValue)
        {
            return false;
        }

        var eventTime = DateTime.SpecifyKind(scheduledEvent.PostedEventTime.Value, DateTimeKind.Utc);
        if (scheduledEvent.LastNotificationEventTime.HasValue
            && scheduledEvent.LastNotificationEventTime.Value == eventTime)
        {
            return false;
        }

        var notificationTime = eventTime.AddMinutes(-scheduledEvent.NotificationMinutesBeforeStart);
        return notificationTime <= now && now < eventTime;
    }

    public static bool HasSignupNotificationResponseTypes(ScheduledEvent scheduledEvent) =>
        ScheduledEventResponseOptions
            .ForEvent(scheduledEvent.EventType, scheduledEvent.ResponseOptionsJson)
            .Any(o => o.Notify);

    public static void MarkSignupNotificationChecked(ScheduledEvent scheduledEvent)
    {
        if (scheduledEvent.PostedEventTime.HasValue)
        {
            scheduledEvent.LastNotificationEventTime = DateTime.SpecifyKind(
                scheduledEvent.PostedEventTime.Value,
                DateTimeKind.Utc);
        }
    }
}
