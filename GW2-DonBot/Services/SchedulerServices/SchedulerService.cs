using Discord;
using Discord.WebSocket;
using DonBot.Models.Entities;
using DonBot.Services.DatabaseServices;
using DonBot.Services.WordleServices;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DonBot.Services.SchedulerServices;

public class SchedulerService(
    IEntityService entityService,
    IWordleService wordleService,
    IWordGeneratorService wordGeneratorService,
    ILogger<SchedulerService> logger,
    DiscordSocketClient client,
    DictionaryService dictionaryService)
    : BackgroundService
{
    private Timer? _timer;
    private Timer? _eventCheckTimer;
    private readonly List<Timer> _eventTimers = [];
    private readonly Dictionary<long, bool> _scheduledEventIds = new();

    public override async Task StopAsync(CancellationToken stoppingToken)
    {
        foreach (var timer in _eventTimers)
        {
            await timer.DisposeAsync();
        }

        _eventTimers.Clear();
        
        _eventCheckTimer?.Dispose();
        
        await base.StopAsync(stoppingToken);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("SchedulerService is starting.");

        await ScheduleEventMessages();
        ScheduleWordleStartingWord();
        
        // Set up a recurring check for new events every hour
        _eventCheckTimer = new Timer(async _ => 
        {
            await CheckForNewEvents();
        }, 
        null, 
        TimeSpan.FromMinutes(15), // First check after 15 minutes
        TimeSpan.FromHours(1));   // Then every hour
    }

    private async Task CheckForNewEvents()
    {
        try
        {
            logger.LogInformation("Checking for new scheduled events...");
            var scheduledEvents = await entityService.ScheduledEvent.GetAllAsync();
            int newEventsScheduled = 0;

            foreach (var scheduledEvent in scheduledEvents)
            {
                if (!_scheduledEventIds.ContainsKey(scheduledEvent.ScheduledEventId))
                {
                    var now = DateTime.UtcNow;
                    var nextEventTime = GetNextEventTime(scheduledEvent, now);

                    if (nextEventTime > now)
                    {
                        await ScheduleSingleEvent(scheduledEvent, nextEventTime, now);
                        newEventsScheduled++;
                    }
                }
            }

            if (newEventsScheduled > 0)
            {
                logger.LogInformation("Scheduled {count} new events found in database.", newEventsScheduled);
            }
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
            {
                await ScheduleSingleEvent(scheduledEvent, nextEventTime, now);
            }
        }
    }

    private Task ScheduleSingleEvent(ScheduledEvent scheduledEvent, DateTime nextEventTime, DateTime now)
    {
        var timeUntilEvent = (nextEventTime - now).TotalMilliseconds;

        var timer = new Timer(_ =>
        {
            Task.Run(() => PostScheduledEventMessage(scheduledEvent))
                .ContinueWith(task =>
                {
                    if (task.Exception != null)
                    {
                        logger.LogError(task.Exception, "Error occurred while posting scheduled event message for event {ScheduledEventId}.", scheduledEvent.ScheduledEventId);
                    }
                });
        }, null, (long)timeUntilEvent, Timeout.Infinite);

        _eventTimers.Add(timer);
        
        _scheduledEventIds[scheduledEvent.ScheduledEventId] = true;
        
        logger.LogInformation("Scheduled event {ScheduledEventId} for {NextEventTime}, which is in {TimeUntilEvent} ms", 
            scheduledEvent.ScheduledEventId, nextEventTime, timeUntilEvent);
        return Task.CompletedTask;
    }

    private DateTime GetNextEventTime(ScheduledEvent scheduledEvent, DateTime now)
    {
        var nextEventTime = new DateTime(
            now.Year,
            now.Month,
            now.Day,
            scheduledEvent.Hour,
            0,
            0,
            DateTimeKind.Utc
        );

        while ((int)nextEventTime.DayOfWeek != scheduledEvent.Day || nextEventTime <= now)
        {
            nextEventTime = nextEventTime.AddDays(1);
        }

        return nextEventTime;
    }

    private async Task PostScheduledEventMessage(ScheduledEvent scheduledEvent)
    {
        var guild = client.GetGuild((ulong)scheduledEvent.GuildId);
        if (guild != null)
        {
            var channel = guild.GetTextChannel((ulong)scheduledEvent.ChannelId);
            if (channel != null)
            {
                try
                {
                    var localTime = scheduledEvent.UtcEventTime.ToLocalTime();
                    var unixTimestamp = new DateTimeOffset(localTime).ToUnixTimeSeconds();
                    var discordTimestamp = $"<t:{unixTimestamp}:f>\n";

                    var embed = new EmbedBuilder()
                        .WithTitle("Event Roster")
                        .WithDescription(string.Empty)
                        .WithColor(Color.Blue);

                    if (scheduledEvent.GuildId != 415441457151737870)
                    {
                        embed
                            .AddField("✅ Roster", "No one has joined yet.", false)
                            .AddField("🛠️ Fillers", "No fillers yet.", false);
                    }

                    embed.AddField("❌ Can't Join", "No one has declined yet.", false);

                    var builtEmbed = embed.Build();

                    var component = new ComponentBuilder()
                        .WithButton("Join", customId: $"join_{scheduledEvent.ScheduledEventId}", ButtonStyle.Success, Emoji.Parse("✅"))
                        .WithButton("Can't Join", customId: $"cantjoin_{scheduledEvent.ScheduledEventId}", ButtonStyle.Secondary, Emoji.Parse("❌"))
                        .WithButton("Can Fill", customId: $"canfill_{scheduledEvent.ScheduledEventId}", ButtonStyle.Primary, Emoji.Parse("🛠️"))
                        .Build();

                    var message = await channel.SendMessageAsync(text: discordTimestamp + scheduledEvent.Message, embed: builtEmbed, components: component);

                    scheduledEvent.MessageId = (long)message.Id;

                    scheduledEvent.UtcEventTime = scheduledEvent.UtcEventTime.AddDays(7);

                    await entityService.ScheduledEvent.UpdateAsync(scheduledEvent);

                    _scheduledEventIds.Remove(scheduledEvent.ScheduledEventId);

                    logger.LogInformation("Posted scheduled message with buttons to channel {ChannelId} in guild {GuildId}.", scheduledEvent.ChannelId, scheduledEvent.GuildId);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to send scheduled message for event {ScheduledEventId}.", scheduledEvent.ScheduledEventId);
                }
            }
            else
            {
                logger.LogWarning("Channel with ID {ChannelId} not found in guild {GuildId}.", scheduledEvent.ChannelId, scheduledEvent.GuildId);
            }
        }
        else
        {
            logger.LogWarning("Guild with ID {GuildId} not found.", scheduledEvent.GuildId);
        }
    }


    private void ScheduleWordleStartingWord()
    {
        var now = DateTime.Now;
        var targetTime = new DateTime(now.Year, now.Month, now.Day, 15, 01, 00);

        if (now > targetTime)
        {
            targetTime = targetTime.AddDays(1);
        }

        var timeUntilTarget = (targetTime - now).TotalMilliseconds;
        _timer = new Timer(OnTimerElapsed, null, (long)timeUntilTarget, Timeout.Infinite);
    }

    private void OnTimerElapsed(object? state)
    {
        TimerElapsedAsync().ConfigureAwait(false);
    }

    private async Task TimerElapsedAsync()
    {
        _timer?.Change(Timeout.Infinite, Timeout.Infinite);

        var wordleWord = await wordleService.FetchWordleWord();
        if (!string.IsNullOrEmpty(wordleWord))
        {
            var startingWord = wordGeneratorService.GenerateStartingWord(wordleWord);
            logger.LogInformation("Generated Starting Word: {startingWord}", startingWord);

            var startingWordDefinition = await dictionaryService.GetDefinitionsAsync(startingWord);

            // Fetch the guild first
            // TODO update this to be config based per guild
            const ulong guildId = 1007536196462854185;
            const ulong channelId = 1021287605897265162;
            const ulong roleId = 1277580524197515336;

            var guild = client.GetGuild(guildId);
            if (guild != null)
            {
                logger.LogInformation("Found guild with ID {guildId}", guildId);
                await Task.Delay(2000);

                // Fetch the channel from the guild
                var channel = guild.GetTextChannel(channelId);
                if (channel != null)
                {
                    logger.LogInformation("Found channel with ID {channelId}", channelId);

                    var roleMention = $"<@&{roleId}>";
                    var message = $"{roleMention} Today's Wordle starting word: {startingWord}{Environment.NewLine}{startingWordDefinition}{Environment.NewLine}https://www.nytimes.com/games/wordle/index.html";
                    try
                    {
                        await channel.SendMessageAsync(message);
                    }
                    catch(Exception ex)
                    {
                        logger.LogError(ex, "Failed to send wordle message.");
                    }
                }
                else
                {
                    logger.LogWarning("Text channel with ID {channelId} not found in guild {guildId}.", channelId, guildId);
                }
            }
            else
            {
                logger.LogWarning("Guild with ID {guildId} not found.", guildId);
            }
        }

        ScheduleWordleStartingWord();
    }
}