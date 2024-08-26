using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DonBotDayOff.Services
{
    public class SchedulerService(IWordleService wordleService, IWordGeneratorService wordGeneratorService, ILogger<SchedulerService> logger, DiscordSocketClient client) : BackgroundService
    {
        // Discord channel ID to send the message to
        private Timer? _timer;

        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            ScheduleWordleStartingWord();
            return Task.CompletedTask;
        }

        private void ScheduleWordleStartingWord()
        {
            var now = DateTime.Now;
            var targetTime = new DateTime(now.Year, now.Month, now.Day, 04, 01, 0);

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

                // Initialize and connect Discord client
                await client.LoginAsync(TokenType.Bot, FetchDonBotToken());
                await client.StartAsync();

                while (client.ConnectionState != ConnectionState.Connected)
                {
                    await Task.Delay(100);
                }

                // Send the message to the specified channel
                // TODO update this to be config based per guild
                if (client.GetChannel(1021287605897265162) is ITextChannel channel)
                {
                    var roleMention = $"<@&{1277580524197515336}>";
                    var message = $"{roleMention} Today's Wordle starting word: {startingWord}";
                    await channel.SendMessageAsync(message);
                }
                else
                {
                    logger.LogWarning($"Channel with ID {1021287605897265162} not found.");
                }

                // Dispose of the Discord client
                await client.StopAsync();
                await client.DisposeAsync();
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
