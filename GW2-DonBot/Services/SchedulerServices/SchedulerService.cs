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
    private Timer? _eventCheckTimer;
    private readonly List<Timer> _eventTimers = [];
    private readonly Dictionary<long, bool> _scheduledEventIds = [];

    public override async Task StopAsync(CancellationToken stoppingToken)
    {
        foreach (var timer in _eventTimers)
            await timer.DisposeAsync();

        _eventTimers.Clear();
        await (_eventCheckTimer?.DisposeAsync() ?? ValueTask.CompletedTask);

        await base.StopAsync(stoppingToken);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("SchedulerService is starting.");

        await ScheduleEventMessages();

        _eventCheckTimer = new Timer(_ => Task.Run(CheckForNewEvents, stoppingToken),
            null,
            TimeSpan.FromMinutes(15),
            TimeSpan.FromHours(1));
    }

    private async Task CheckForNewEvents()
    {
        try
        {
            logger.LogInformation("Checking for new scheduled events...");
            var scheduledEvents = await entityService.ScheduledEvent.GetAllAsync();
            var newEventsScheduled = 0;

            foreach (var scheduledEvent in scheduledEvents)
            {
                if (_scheduledEventIds.ContainsKey(scheduledEvent.ScheduledEventId))
                    continue;

                var now = DateTime.UtcNow;
                var nextEventTime = GetNextEventTime(scheduledEvent, now);
                if (nextEventTime > now)
                {
                    await ScheduleSingleEvent(scheduledEvent, nextEventTime, now);
                    newEventsScheduled++;
                }
            }

            if (newEventsScheduled > 0)
                logger.LogInformation("Scheduled {Count} new events found in database.", newEventsScheduled);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error occurred while checking for new events.");
        }
    }

    private async Task ScheduleEventMessages()
    {
        var scheduledEvents = await entityService.ScheduledEvent.GetAllAsync();
        _scheduledEventIds.Clear();

        foreach (var scheduledEvent in scheduledEvents)
        {
            var now = DateTime.UtcNow;
            var nextEventTime = GetNextEventTime(scheduledEvent, now);
            if (nextEventTime > now)
                await ScheduleSingleEvent(scheduledEvent, nextEventTime, now);
        }
    }

    private Task ScheduleSingleEvent(ScheduledEvent scheduledEvent, DateTime nextEventTime, DateTime now)
    {
        var timeUntilEvent = (nextEventTime - now).TotalMilliseconds;

        var timer = new Timer(_ =>
        {
            Task.Run(() => FireEvent(scheduledEvent))
                .ContinueWith(task =>
                {
                    if (task.Exception != null)
                        logger.LogError(task.Exception, "Error posting scheduled event {ScheduledEventId}.", scheduledEvent.ScheduledEventId);
                });
        }, null, (long)timeUntilEvent, Timeout.Infinite);

        _eventTimers.Add(timer);
        _scheduledEventIds[scheduledEvent.ScheduledEventId] = true;

        logger.LogInformation("Scheduled event {ScheduledEventId} (type={EventType}) for {NextEventTime} in {TimeUntilEvent:F0} ms.",
            scheduledEvent.ScheduledEventId, (ScheduledEventTypeEnum)scheduledEvent.EventType, nextEventTime, timeUntilEvent);

        return Task.CompletedTask;
    }

    private DateTime GetNextEventTime(ScheduledEvent scheduledEvent, DateTime now)
    {
        var nextEventTime = new DateTime(now.Year, now.Month, now.Day, scheduledEvent.Hour, 0, 0, DateTimeKind.Utc);

        // Daily: just advance to tomorrow if today's slot has passed.
        if (scheduledEvent.RepeatIntervalDays == 1)
        {
            if (nextEventTime <= now) nextEventTime = nextEventTime.AddDays(1);
            return nextEventTime;
        }

        // Weekly: advance until we hit the configured day of week at the right hour.
        while ((int)nextEventTime.DayOfWeek != scheduledEvent.Day || nextEventTime <= now)
            nextEventTime = nextEventTime.AddDays(1);

        return nextEventTime;
    }

    private async Task FireEvent(ScheduledEvent scheduledEvent)
    {
        var eventType = (ScheduledEventTypeEnum)scheduledEvent.EventType;
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

        try
        {
            await handler.HandleAsync(scheduledEvent, socketGuild);

            scheduledEvent.UtcEventTime = scheduledEvent.UtcEventTime.AddDays(scheduledEvent.RepeatIntervalDays);
            await entityService.ScheduledEvent.UpdateAsync(scheduledEvent);
            _scheduledEventIds.Remove(scheduledEvent.ScheduledEventId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Handler for {EventType} failed on event {ScheduledEventId}.", eventType, scheduledEvent.ScheduledEventId);
        }
    }
}
