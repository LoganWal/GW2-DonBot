using DonBot.Core.Models.Entities;
using DonBot.Core.Models.Scheduling;

namespace DonBot.Core.Services.Scheduling;

public static class ScheduledEventPlanner
{
    public static ScheduledEvent BuildEvent(long guildId, ScheduledEventWriteRequest request)
    {
        var scheduledEvent = new ScheduledEvent
        {
            GuildId = guildId
        };
        ApplyForCreate(scheduledEvent, request);
        return scheduledEvent;
    }

    public static void ApplyForCreate(ScheduledEvent scheduledEvent, ScheduledEventWriteRequest request)
    {
        Apply(scheduledEvent, request, ScheduledEventResponseOptions.SerializeForEventType(
            request.EventType,
            request.ResponseOptions));
    }

    public static void ApplyForUpdate(ScheduledEvent scheduledEvent, ScheduledEventWriteRequest request)
    {
        Apply(scheduledEvent, request, ResolveResponseOptionsJsonForUpdate(scheduledEvent, request));
    }

    public static string ResolveResponseOptionsJsonForUpdate(
        ScheduledEvent existing,
        ScheduledEventWriteRequest request)
    {
        if (!ScheduledEventResponseOptions.IsSignupEvent(request.EventType))
        {
            return string.Empty;
        }

        if (request.ResponseOptions is not null)
        {
            return ScheduledEventResponseOptions.SerializeForEventType(request.EventType, request.ResponseOptions);
        }

        if (ScheduledEventResponseOptions.IsSignupEvent(existing.EventType))
        {
            var existingOptions = ScheduledEventResponseOptions.ForEvent(
                existing.EventType,
                existing.ResponseOptionsJson);
            return ScheduledEventResponseOptions.Serialize(existingOptions);
        }

        return ScheduledEventResponseOptions.SerializeForEventType(request.EventType, null);
    }

    private static void Apply(
        ScheduledEvent scheduledEvent,
        ScheduledEventWriteRequest request,
        string responseOptionsJson)
    {
        scheduledEvent.EventType = request.EventType;
        scheduledEvent.ChannelId = request.ChannelId;
        scheduledEvent.Day = request.Day;
        scheduledEvent.Hour = request.Hour;
        scheduledEvent.RepeatIntervalDays = request.RepeatIntervalDays;
        scheduledEvent.NotificationMinutesBeforeStart = request.NotificationMinutesBeforeStart;
        scheduledEvent.Message = (request.Message ?? string.Empty).Trim();
        scheduledEvent.ResponseOptionsJson = responseOptionsJson;
        scheduledEvent.UtcEventTime = DateTime.SpecifyKind(request.UtcEventTime, DateTimeKind.Utc);
    }
}
