using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DonBotDayOff.Services
{
    public class SchedulerService(IWordleService wordleService, IWordGeneratorService wordGeneratorService, ILogger<SchedulerService> logger, DiscordSocketClient client, DictionaryService dictionaryService) : BackgroundService
    {
        private Timer? _timer;

        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
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
            _timer?.Change(Timeout.Infinite, Timeout.Infinite); // Stop the timer

            var wordleWord = await wordleService.FetchWordleWord();
            if (!string.IsNullOrEmpty(wordleWord))
            {
                var startingWord = wordGeneratorService.GenerateStartingWord(wordleWord);
                logger.LogInformation($"Generated Starting Word: {startingWord}");

                var startingWordDefinition = await dictionaryService.GetDefinitionsAsync(startingWord);

                // Initialize and connect Discord client
                if (client.ConnectionState != ConnectionState.Connected)
                {
                    logger.LogInformation("Attempting to log in to Discord");
                    await client.LoginAsync(TokenType.Bot, FetchDonBotToken());
                    logger.LogInformation("Login to Discord successful");

                    logger.LogInformation("Attempting to start Discord client");
                    await client.StartAsync();
                }

                logger.LogInformation("Attempting to connect via discord client");
                while (client.ConnectionState != ConnectionState.Connected)
                {
                    logger.LogInformation("Not connected, trying again after 1s");
                    await Task.Delay(1000);
                }
                logger.LogInformation("Discord client connected");

                // Fetch the guild first
                // TODO update this to be config based per guild
                ulong guildId = 1007536196462854185;
                ulong channelId = 1021287605897265162;
                ulong roleId = 1277580524197515336;

                var guild = client.GetGuild(guildId);
                if (guild != null)
                {
                    logger.LogInformation($"Found guild with ID {guildId}");
                    await Task.Delay(2000);

                    // Fetch the channel from the guild
                    var channel = guild.GetTextChannel(channelId);
                    if (channel != null)
                    {
                        logger.LogInformation($"Found channel with ID {channelId}");

                        var roleMention = $"<@&{roleId}>";
                        var message = $"{roleMention} Today's Wordle starting word: {startingWord}{Environment.NewLine}{startingWordDefinition}{Environment.NewLine}https://www.nytimes.com/games/wordle/index.html";
                        await channel.SendMessageAsync(message);
                    }
                    else
                    {
                        logger.LogWarning($"Text channel with ID {channelId} not found in guild {guildId}.");
                    }
                }
                else
                {
                    logger.LogWarning($"Guild with ID {guildId} not found.");
                }
            }

            // Reschedule the task for the next day
            ScheduleWordleStartingWord();
        }

        private static string FetchDonBotToken()
        {
            var donBotToken = Environment.GetEnvironmentVariable("DonBotToken", EnvironmentVariableTarget.User);

            if (string.IsNullOrEmpty(donBotToken))
            {
                throw new Exception("DonBotToken does not exist");
            }

            return donBotToken;
        }
    }
}
