using System.Collections.Concurrent;
using Discord;
using Discord.WebSocket;
using DonBot.Models.Entities;
using DonBot.Models.Enums;
using DonBot.Models.Scheduling;
using DonBot.Services.DatabaseServices;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DonBot.Services.SchedulerServices;

public sealed class SchedulerService(
    IEntityService entityService,
    ILogger<SchedulerService> logger,
    DiscordSocketClient client,
    IEnumerable<IScheduledEventHandler> eventHandlers)
    : BackgroundService
{
    private readonly ConcurrentDictionary<long, (ScheduledEvent Event, DateTime NextFireTime)> _scheduledEvents = new();

    private sealed record ScheduledEventSnapshot(
        long ScheduledEventId,
        long GuildId,
        short EventType,
        long ChannelId,
        long? MessageId,
        short Day,
        short Hour,
        DateTime UtcEventTime,
        short RepeatIntervalDays,
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
                scheduledEvent.Day,
                scheduledEvent.Hour,
                scheduledEvent.UtcEventTime,
                scheduledEvent.RepeatIntervalDays,
                scheduledEvent.Message,
                scheduledEvent.ResponseOptionsJson);

        public bool Matches(ScheduledEvent scheduledEvent) =>
            ScheduledEventId == scheduledEvent.ScheduledEventId
            && GuildId == scheduledEvent.GuildId
            && EventType == scheduledEvent.EventType
            && ChannelId == scheduledEvent.ChannelId
            && MessageId == scheduledEvent.MessageId
            && Day == scheduledEvent.Day
            && Hour == scheduledEvent.Hour
            && UtcEventTime == scheduledEvent.UtcEventTime
            && RepeatIntervalDays == scheduledEvent.RepeatIntervalDays
            && Message == scheduledEvent.Message
            && ResponseOptionsJson == scheduledEvent.ResponseOptionsJson;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("SchedulerService is starting.");

        try
        {
            await LoadScheduledEvents();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to load scheduled events on startup.");
        }

        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(30));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                var now = DateTime.UtcNow;
                await CheckForNewEvents(now);
                FireDueEvents(now);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error in scheduler loop.");
            }
        }
    }

    private async Task LoadScheduledEvents()
    {
        var scheduledEvents = await entityService.ScheduledEvent.GetAllAsync();
        _scheduledEvents.Clear();

        var now = DateTime.UtcNow;
        foreach (var scheduledEvent in scheduledEvents)
        {
            await FastForwardEventIfBehind(scheduledEvent, now);
            var nextFireTime = GetNextEventTime(scheduledEvent, now);
            _scheduledEvents[scheduledEvent.ScheduledEventId] = (scheduledEvent, nextFireTime);
            logger.LogInformation("Scheduled event {ScheduledEventId} (type={EventType}) for {NextFireTime}.",
                scheduledEvent.ScheduledEventId, (ScheduledEventTypeEnum)scheduledEvent.EventType, nextFireTime);
        }

    }

    private async Task CheckForNewEvents(DateTime now)
    {
        try
        {
            logger.LogInformation("Checking for new scheduled events...");
            var scheduledEvents = await entityService.ScheduledEvent.GetAllAsync();
            var newCount = 0;
            var removedCount = 0;
            var currentIds = scheduledEvents.Select(e => e.ScheduledEventId).ToHashSet();

            foreach (var scheduledEventId in _scheduledEvents.Keys.Where(id => !currentIds.Contains(id)).ToList())
            {
                if (_scheduledEvents.TryRemove(scheduledEventId, out _))
                {
                    removedCount++;
                    logger.LogInformation("Removed deleted scheduled event {ScheduledEventId} from scheduler cache.", scheduledEventId);
                }
            }

            foreach (var scheduledEvent in scheduledEvents)
            {
                if (_scheduledEvents.TryGetValue(scheduledEvent.ScheduledEventId, out var cached))
                {
                    if (cached.NextFireTime != DateTime.MaxValue && HasScheduledEventChanged(cached.Event, scheduledEvent))
                    {
                        await FastForwardEventIfBehind(scheduledEvent, now);
                        var refreshedNextFireTime = GetNextEventTime(scheduledEvent, now);
                        _scheduledEvents[scheduledEvent.ScheduledEventId] = (scheduledEvent, refreshedNextFireTime);

                        logger.LogInformation("Updated scheduled event {ScheduledEventId} in scheduler cache for {NextFireTime}.",
                            scheduledEvent.ScheduledEventId, refreshedNextFireTime);
                    }

                    continue;
                }

                await FastForwardEventIfBehind(scheduledEvent, now);
                var nextFireTime = GetNextEventTime(scheduledEvent, now);
                _scheduledEvents[scheduledEvent.ScheduledEventId] = (scheduledEvent, nextFireTime);
                newCount++;

                logger.LogInformation("New event detected: Scheduled event {ScheduledEventId} (type={EventType}) for {NextFireTime}.",
                    scheduledEvent.ScheduledEventId, (ScheduledEventTypeEnum)scheduledEvent.EventType, nextFireTime);
            }

            if (newCount > 0)
            {
                logger.LogInformation("Scheduled {Count} new events found in database.", newCount);
            }
            if (removedCount > 0)
            {
                logger.LogInformation("Removed {Count} deleted scheduled events from scheduler cache.", removedCount);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error occurred while checking for new events.");
        }
    }

    private void FireDueEvents(DateTime now)
    {
        var dueEvents = _scheduledEvents
            .Where(kvp => kvp.Value.NextFireTime <= now && kvp.Value.NextFireTime != DateTime.MaxValue)
            .OrderBy(kvp => kvp.Value.Event.UtcEventTime)
            .ToList();

        foreach (var (id, (scheduledEvent, _)) in dueEvents)
        {
            // Prevent double-firing while the background task runs.
            _scheduledEvents[id] = (scheduledEvent, DateTime.MaxValue);
        }

        _ = Task.Run(async () =>
        {
            foreach (var (_, (scheduledEvent, nextFireTime)) in dueEvents)
            {
                await FireEvent(scheduledEvent, nextFireTime);
            }
        });
    }

    private async Task FireEvent(ScheduledEvent scheduledEvent, DateTime selectedFireTime)
    {
        var eventType = (ScheduledEventTypeEnum)scheduledEvent.EventType;

        try
        {
            var currentScheduledEvent = await entityService.ScheduledEvent.GetFirstOrDefaultAsync(
                e => e.ScheduledEventId == scheduledEvent.ScheduledEventId);
            if (currentScheduledEvent is null)
            {
                _scheduledEvents.TryRemove(scheduledEvent.ScheduledEventId, out _);
                logger.LogInformation("Scheduled event {ScheduledEventId} was removed before firing.", scheduledEvent.ScheduledEventId);
                return;
            }

            if (HasScheduledEventChanged(scheduledEvent, currentScheduledEvent))
            {
                scheduledEvent = currentScheduledEvent;
                eventType = (ScheduledEventTypeEnum)scheduledEvent.EventType;
            }

            if (!IsScheduledForFireTime(scheduledEvent, selectedFireTime))
            {
                logger.LogInformation(
                    "Scheduled event {ScheduledEventId} changed after it was selected for {SelectedFireTime}; skipping stale fire.",
                    scheduledEvent.ScheduledEventId,
                    selectedFireTime);
                return;
            }

            var preFireSnapshot = ScheduledEventSnapshot.Capture(scheduledEvent);

            // UtcEventTime is shown in messages, so advance stale rows before sending.
            var fireNow = DateTime.UtcNow;
            if (scheduledEvent.UtcEventTime <= fireNow)
            {
                logger.LogWarning("Event {ScheduledEventId} has stale UtcEventTime {UtcEventTime} at fire time - fast-forwarding before sending.",
                    scheduledEvent.ScheduledEventId, scheduledEvent.UtcEventTime);
                AdvanceEventIfBehind(scheduledEvent, fireNow);
            }

            var handlerEventType = (ScheduledEventTypeEnum)ScheduledEventResponseOptions.ToCurrentEventType(scheduledEvent.EventType);
            var handler = eventHandlers.FirstOrDefault(h => h.EventType == handlerEventType);
            if (handler == null)
            {
                logger.LogWarning("No handler registered for event type {EventType}.", eventType);
                return;
            }

            var socketGuild = client.GetGuild((ulong)scheduledEvent.GuildId);
            if (socketGuild == null)
            {
                logger.LogWarning("Guild {GuildId} not found for event {ScheduledEventId}.", scheduledEvent.GuildId, scheduledEvent.ScheduledEventId);
                return;
            }

            await handler.HandleAsync(scheduledEvent, socketGuild);

            var currentBeforeSave = await entityService.ScheduledEvent.GetFirstOrDefaultAsync(
                e => e.ScheduledEventId == scheduledEvent.ScheduledEventId);
            if (currentBeforeSave is null)
            {
                await DeletePostedSignupMessageIfNewAsync(scheduledEvent, preFireSnapshot, socketGuild);
                _scheduledEvents.TryRemove(scheduledEvent.ScheduledEventId, out _);
                logger.LogInformation("Scheduled event {ScheduledEventId} was removed while firing; posted signup message was cleaned up if needed.",
                    scheduledEvent.ScheduledEventId);
                return;
            }

            if (!preFireSnapshot.Matches(currentBeforeSave))
            {
                await DeletePostedSignupMessageIfNewAsync(scheduledEvent, preFireSnapshot, socketGuild);
                scheduledEvent = currentBeforeSave;
                logger.LogInformation("Scheduled event {ScheduledEventId} changed while firing; skipped stale scheduler save.",
                    scheduledEvent.ScheduledEventId);
                return;
            }

            currentBeforeSave.MessageId = scheduledEvent.MessageId;
            currentBeforeSave.UtcEventTime = scheduledEvent.UtcEventTime.AddDays(currentBeforeSave.RepeatIntervalDays);
            currentBeforeSave.Day = (short)((scheduledEvent.Day + currentBeforeSave.RepeatIntervalDays) % 7);
            await entityService.ScheduledEvent.UpdateAsync(currentBeforeSave);
            scheduledEvent = currentBeforeSave;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Handler for {EventType} failed on event {ScheduledEventId}.", eventType, scheduledEvent.ScheduledEventId);
        }
        finally
        {
            await RescheduleIfStillCurrent(scheduledEvent);
        }
    }

    private async Task RescheduleIfStillCurrent(ScheduledEvent scheduledEvent)
    {
        try
        {
            var currentScheduledEvent = await entityService.ScheduledEvent.GetFirstOrDefaultAsync(
                e => e.ScheduledEventId == scheduledEvent.ScheduledEventId);
            if (currentScheduledEvent is null)
            {
                _scheduledEvents.TryRemove(scheduledEvent.ScheduledEventId, out _);
                logger.LogInformation("Scheduled event {ScheduledEventId} was removed while firing and will not be re-cached.",
                    scheduledEvent.ScheduledEventId);
                return;
            }

            if (HasScheduledEventChanged(scheduledEvent, currentScheduledEvent))
            {
                scheduledEvent = currentScheduledEvent;
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to verify scheduled event {ScheduledEventId} after firing; keeping in-memory state.",
                scheduledEvent.ScheduledEventId);
        }

        var now = DateTime.UtcNow;
        var nextFireTime = GetNextEventTime(scheduledEvent, now);
        _scheduledEvents[scheduledEvent.ScheduledEventId] = (scheduledEvent, nextFireTime);
        logger.LogInformation("Scheduled event {ScheduledEventId} (type={EventType}) for {NextFireTime}.",
            scheduledEvent.ScheduledEventId, (ScheduledEventTypeEnum)scheduledEvent.EventType, nextFireTime);
    }

    private static bool HasScheduledEventChanged(ScheduledEvent cached, ScheduledEvent current) =>
        cached.EventType != current.EventType
        || cached.GuildId != current.GuildId
        || cached.ChannelId != current.ChannelId
        || cached.MessageId != current.MessageId
        || cached.Day != current.Day
        || cached.Hour != current.Hour
        || cached.UtcEventTime != current.UtcEventTime
        || cached.RepeatIntervalDays != current.RepeatIntervalDays
        || cached.Message != current.Message
        || cached.ResponseOptionsJson != current.ResponseOptionsJson;

    private async Task DeletePostedSignupMessageIfNewAsync(
        ScheduledEvent scheduledEvent,
        ScheduledEventSnapshot preFireSnapshot,
        SocketGuild socketGuild)
    {
        if (!ScheduledEventResponseOptions.IsSignupEvent(scheduledEvent.EventType)
            || !scheduledEvent.MessageId.HasValue
            || scheduledEvent.MessageId == preFireSnapshot.MessageId)
        {
            return;
        }

        try
        {
            var channel = socketGuild.GetTextChannel((ulong)scheduledEvent.ChannelId);
            if (channel is null)
            {
                return;
            }

            var message = await channel.GetMessageAsync((ulong)scheduledEvent.MessageId.Value);
            if (message is not null)
            {
                await message.DeleteAsync();
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to clean up posted signup message {MessageId} for stale event {ScheduledEventId}.",
                scheduledEvent.MessageId, scheduledEvent.ScheduledEventId);
        }
    }

    internal async Task FastForwardEventIfBehind(ScheduledEvent scheduledEvent, DateTime now)
    {
        if (!AdvanceEventIfBehind(scheduledEvent, now))
        {
            return;
        }

        try
        {
            await entityService.ScheduledEvent.UpdateAsync(scheduledEvent);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to persist fast-forward for event {ScheduledEventId} - in-memory state is correct but DB may be stale.", scheduledEvent.ScheduledEventId);
        }

        logger.LogInformation("Fast-forwarded event {ScheduledEventId} to UtcEventTime={UtcEventTime} Day={Day}.",
            scheduledEvent.ScheduledEventId, scheduledEvent.UtcEventTime, scheduledEvent.Day);
    }

    private static bool AdvanceEventIfBehind(ScheduledEvent scheduledEvent, DateTime now)
    {
        if (scheduledEvent.RepeatIntervalDays <= 0 || scheduledEvent.UtcEventTime > now)
        {
            return false;
        }

        while (scheduledEvent.UtcEventTime <= now)
        {
            scheduledEvent.UtcEventTime = scheduledEvent.UtcEventTime.AddDays(scheduledEvent.RepeatIntervalDays);
            scheduledEvent.Day = (short)((scheduledEvent.Day + scheduledEvent.RepeatIntervalDays) % 7);
        }

        return true;
    }

    internal static DateTime GetNextEventTime(ScheduledEvent scheduledEvent, DateTime now)
    {
        if (scheduledEvent.RepeatIntervalDays <= 0)
        {
            return DateTime.MaxValue;
        }

        var nextEventTime = new DateTime(now.Year, now.Month, now.Day, scheduledEvent.Hour, 0, 0, DateTimeKind.Utc);
        while ((int)nextEventTime.DayOfWeek != scheduledEvent.Day || nextEventTime <= now)
        {
            nextEventTime = nextEventTime.AddDays(1);
        }

        return nextEventTime;
    }

    internal static bool IsScheduledForFireTime(ScheduledEvent scheduledEvent, DateTime fireTime) =>
        scheduledEvent.RepeatIntervalDays > 0
        && scheduledEvent.Day == (short)fireTime.DayOfWeek
        && scheduledEvent.Hour == fireTime.Hour;
}
