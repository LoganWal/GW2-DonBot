using Discord.WebSocket;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DonBot.Services.WordleServices
{
    public class SchedulerService : BackgroundService
    {
        private Timer? _timer;

        private readonly IWordleService _wordleService;
        private readonly IWordGeneratorService _wordGeneratorService;
        private readonly ILogger<SchedulerService> _logger;
        private readonly DiscordSocketClient _client;
        private readonly DictionaryService _dictionaryService;

        public SchedulerService(
            IWordleService wordleService, 
            IWordGeneratorService wordGeneratorService, 
            ILogger<SchedulerService> logger, 
            DiscordSocketClient client, 
            DictionaryService dictionaryService)
        {
            _wordleService = wordleService;
            _wordGeneratorService = wordGeneratorService;
            _logger = logger;
            _client = client;
            _dictionaryService = dictionaryService;
        }

        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("SchedulerService is starting.");
            ScheduleWordleStartingWord();
            return Task.CompletedTask;
        }

        private void ScheduleWordleStartingWord()
        {
            var now = DateTime.Now;
            var targetTime = new DateTime(now.Year, now.Month, now.Day, 14, 01, 00);

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

            var wordleWord = await _wordleService.FetchWordleWord();
            if (!string.IsNullOrEmpty(wordleWord))
            {
                var startingWord = _wordGeneratorService.GenerateStartingWord(wordleWord);
                _logger.LogInformation("Generated Starting Word: {startingWord}", startingWord);

                var startingWordDefinition = await _dictionaryService.GetDefinitionsAsync(startingWord);

                // Fetch the guild first
                // TODO update this to be config based per guild
                const ulong guildId = 1007536196462854185;
                const ulong channelId = 1021287605897265162;
                const ulong roleId = 1277580524197515336;

                var guild = _client.GetGuild(guildId);
                if (guild != null)
                {
                    _logger.LogInformation("Found guild with ID {guildId}", guildId);
                    await Task.Delay(2000);

                    // Fetch the channel from the guild
                    var channel = guild.GetTextChannel(channelId);
                    if (channel != null)
                    {
                        _logger.LogInformation("Found channel with ID {channelId}", channelId);

                        var roleMention = $"<@&{roleId}>";
                        var message = $"{roleMention} Today's Wordle starting word: {startingWord}{Environment.NewLine}{startingWordDefinition}{Environment.NewLine}https://www.nytimes.com/games/wordle/index.html";
                        await channel.SendMessageAsync(message);
                    }
                    else
                    {
                        _logger.LogWarning("Text channel with ID {channelId} not found in guild {guildId}.", channelId, guildId);
                    }
                }
                else
                {
                    _logger.LogWarning("Guild with ID {guildId} not found.", guildId);
                }
            }
        }
    }
}
