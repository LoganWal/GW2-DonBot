using System.Collections.Concurrent;
using Discord.WebSocket;
using DonBot.Models.Entities;
using DonBot.Models.Enums;
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
    private DateTime _lastNewEventCheck = DateTime.MinValue;

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
                FireDueEvents(now);

                if (now - _lastNewEventCheck > TimeSpan.FromMinutes(15))
                {
                    await CheckForNewEvents(now);
                }
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

        _lastNewEventCheck = now;
    }

    private async Task CheckForNewEvents(DateTime now)
    {
        try
        {
            logger.LogInformation("Checking for new scheduled events...");
            var scheduledEvents = await entityService.ScheduledEvent.GetAllAsync();
            var newCount = 0;

            foreach (var scheduledEvent in scheduledEvents)
            {
                if (_scheduledEvents.ContainsKey(scheduledEvent.ScheduledEventId))
                {
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
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error occurred while checking for new events.");
        }
        finally
        {
            _lastNewEventCheck = now;
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
            // Mark as in-flight to prevent double-firing
            _scheduledEvents[id] = (scheduledEvent, DateTime.MaxValue);
        }

        _ = Task.Run(async () =>
        {
            foreach (var (_, (scheduledEvent, _)) in dueEvents)
            {
                await FireEvent(scheduledEvent);
            }
        });
    }

    private async Task FireEvent(ScheduledEvent scheduledEvent)
    {
        var eventType = (ScheduledEventTypeEnum)scheduledEvent.EventType;

        try
        {
            // UtcEventTime is the time shown in the message. Guard against it being stale
            // (e.g. bot delayed between scheduling and firing, or DB out of sync).
            var fireNow = DateTime.UtcNow;
            if (scheduledEvent.UtcEventTime <= fireNow)
            {
                logger.LogWarning("Event {ScheduledEventId} has stale UtcEventTime {UtcEventTime} at fire time - fast-forwarding before sending.",
                    scheduledEvent.ScheduledEventId, scheduledEvent.UtcEventTime);
                await FastForwardEventIfBehind(scheduledEvent, fireNow);
            }

            var handler = eventHandlers.FirstOrDefault(h => h.EventType == eventType);
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

            scheduledEvent.UtcEventTime = scheduledEvent.UtcEventTime.AddDays(scheduledEvent.RepeatIntervalDays);
            scheduledEvent.Day = (short)((scheduledEvent.Day + scheduledEvent.RepeatIntervalDays) % 7);
            await entityService.ScheduledEvent.UpdateAsync(scheduledEvent);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Handler for {EventType} failed on event {ScheduledEventId}.", eventType, scheduledEvent.ScheduledEventId);
        }
        finally
        {
            var now = DateTime.UtcNow;
            var nextFireTime = GetNextEventTime(scheduledEvent, now);
            _scheduledEvents[scheduledEvent.ScheduledEventId] = (scheduledEvent, nextFireTime);
            logger.LogInformation("Scheduled event {ScheduledEventId} (type={EventType}) for {NextFireTime}.",
                scheduledEvent.ScheduledEventId, (ScheduledEventTypeEnum)scheduledEvent.EventType, nextFireTime);
        }
    }

    internal async Task FastForwardEventIfBehind(ScheduledEvent scheduledEvent, DateTime now)
    {
        if (scheduledEvent.RepeatIntervalDays <= 0 || scheduledEvent.UtcEventTime > now)
        {
            return;
        }

        while (scheduledEvent.UtcEventTime <= now)
        {
            scheduledEvent.UtcEventTime = scheduledEvent.UtcEventTime.AddDays(scheduledEvent.RepeatIntervalDays);
            scheduledEvent.Day = (short)((scheduledEvent.Day + scheduledEvent.RepeatIntervalDays) % 7);
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
}
