using DonBot.Core.Models.Entities;

namespace DonBot.Core.Services.Scheduling;

public static class ScheduledEventRecurrence
{
    public static DateTime GetNextEventTime(ScheduledEvent scheduledEvent, DateTime now)
    {
        if (scheduledEvent.RepeatIntervalDays <= 0)
        {
            return DateTime.MaxValue;
        }

        var postTime = GetPostTime(scheduledEvent);
        if (postTime > now)
        {
            return postTime;
        }

        var intervals = (now - postTime).Days / scheduledEvent.RepeatIntervalDays + 1;
        return postTime.AddDays(intervals * scheduledEvent.RepeatIntervalDays);
    }

    public static bool IsScheduledForFireTime(ScheduledEvent scheduledEvent, DateTime fireTime) =>
        scheduledEvent.RepeatIntervalDays > 0
        && GetNextEventTime(scheduledEvent, fireTime.AddTicks(-1)) == fireTime;

    public static bool AdvanceEventIfBehind(ScheduledEvent scheduledEvent, DateTime now)
    {
        if (scheduledEvent.RepeatIntervalDays <= 0)
        {
            return false;
        }

        var postTime = GetPostTime(scheduledEvent);
        if (postTime > now)
        {
            return false;
        }

        while (postTime <= now)
        {
            AdvanceByInterval(scheduledEvent, scheduledEvent.RepeatIntervalDays);
            postTime = postTime.AddDays(scheduledEvent.RepeatIntervalDays);
        }

        return true;
    }

    private static DateTime GetPostTime(ScheduledEvent scheduledEvent)
    {
        var eventTime = scheduledEvent.UtcEventTime;
        var daysSincePostDay = ((int)eventTime.DayOfWeek - scheduledEvent.Day + 7) % 7;
        var postTime = eventTime.Date.AddDays(-daysSincePostDay).AddHours(scheduledEvent.Hour);
        // The latest matching slot before the event anchors the entire cadence.
        return postTime > eventTime ? postTime.AddDays(-7) : postTime;
    }

    public static void AdvanceAfterFire(ScheduledEvent scheduledEvent, ScheduledEvent firedOccurrence)
    {
        scheduledEvent.UtcEventTime = firedOccurrence.UtcEventTime;
        scheduledEvent.Day = firedOccurrence.Day;
        AdvanceByInterval(scheduledEvent, scheduledEvent.RepeatIntervalDays);
    }

    private static void AdvanceByInterval(ScheduledEvent scheduledEvent, short repeatIntervalDays)
    {
        scheduledEvent.UtcEventTime = scheduledEvent.UtcEventTime.AddDays(repeatIntervalDays);
        scheduledEvent.Day = (short)((scheduledEvent.Day + repeatIntervalDays) % 7);
    }
}
