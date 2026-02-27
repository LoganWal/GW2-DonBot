using DonBot.Controller.Discord;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DonBot;

internal sealed class DiscordCoreHostedService(IDiscordCore discordCore, ILogger<DiscordCoreHostedService> logger) : IHostedService
{
    private Task? _executingTask;
    private CancellationTokenSource? _stoppingCts;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("DiscordCoreHostedService starting");
        
        _stoppingCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _executingTask = discordCore.MainAsync(_stoppingCts.Token);
        
        if (_executingTask.IsCompleted)
        {
            return _executingTask;
        }
        
        logger.LogInformation("DiscordCoreHostedService started");
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("DiscordCoreHostedService stopping");
        
        if (_executingTask is null)
        {
            return;
        }

        try
        {
            if (_stoppingCts is not null)
            {
                await _stoppingCts.CancelAsync();
                await Task.WhenAny(_executingTask, Task.Delay(TimeSpan.FromSeconds(10), cancellationToken));
            }
        }
        catch (OperationCanceledException)
        {
            // Expected when cancellation is requested
            logger.LogDebug("Cancellation requested during StopAsync");
        }
        finally
        {
            _stoppingCts?.Dispose();
            logger.LogInformation("DiscordCoreHostedService stopped");
        }
    }
}