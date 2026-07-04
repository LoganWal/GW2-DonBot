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

        var nextEventTime = new DateTime(
            now.Year,
            now.Month,
            now.Day,
            scheduledEvent.Hour,
            0,
            0,
            DateTimeKind.Utc);
        while ((int)nextEventTime.DayOfWeek != scheduledEvent.Day || nextEventTime <= now)
        {
            nextEventTime = nextEventTime.AddDays(1);
        }

        return nextEventTime;
    }

    public static bool IsScheduledForFireTime(ScheduledEvent scheduledEvent, DateTime fireTime) =>
        scheduledEvent.RepeatIntervalDays > 0
        && scheduledEvent.Day == (short)fireTime.DayOfWeek
        && scheduledEvent.Hour == fireTime.Hour;

    public static bool AdvanceEventIfBehind(ScheduledEvent scheduledEvent, DateTime now)
    {
        if (scheduledEvent.RepeatIntervalDays <= 0 || scheduledEvent.UtcEventTime > now)
        {
            return false;
        }

        while (scheduledEvent.UtcEventTime <= now)
        {
            AdvanceByInterval(scheduledEvent, scheduledEvent.RepeatIntervalDays);
        }

        return true;
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
