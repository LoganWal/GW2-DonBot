using Discord.WebSocket;
using DonBot.Models.Entities;
using DonBot.Models.Enums;
using DonBot.Services.WordleServices;
using Microsoft.Extensions.Logging;

namespace DonBot.Services.SchedulerServices;

public sealed class WordleEventHandler(
    IWordleService wordleService,
    IWordGeneratorService wordGeneratorService,
    DictionaryService dictionaryService,
    ILogger<WordleEventHandler> logger) : IScheduledEventHandler
{
    public ScheduledEventTypeEnum EventType => ScheduledEventTypeEnum.Wordle;

    public async Task HandleAsync(ScheduledEvent scheduledEvent, SocketGuild socketGuild)
    {
        var channel = socketGuild.GetTextChannel((ulong)scheduledEvent.ChannelId);
        if (channel == null)
        {
            logger.LogWarning("Channel {ChannelId} not found for Wordle.", scheduledEvent.ChannelId);
            return;
        }

        var wordleWord = await wordleService.FetchWordleWord();
        if (string.IsNullOrEmpty(wordleWord))
            return;

        var startingWord = wordGeneratorService.GenerateStartingWord(wordleWord);
        logger.LogInformation("Generated Starting Word: {StartingWord}", startingWord);

        var startingWordDefinition = await dictionaryService.GetDefinitionsAsync(startingWord);

        await Task.Delay(2000);

        var roleMention = scheduledEvent.RoleId.HasValue ? $"<@&{scheduledEvent.RoleId}> " : string.Empty;
        var message = $"{roleMention}Today's Wordle starting word: {startingWord}{Environment.NewLine}{startingWordDefinition}{Environment.NewLine}https://www.nytimes.com/games/wordle/index.html";

        await channel.SendMessageAsync(message);

        logger.LogInformation("Posted Wordle message to channel {ChannelId} in guild {GuildId}.", scheduledEvent.ChannelId, scheduledEvent.GuildId);
    }
}
