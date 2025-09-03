using DonBot.Controller.Discord;
using Microsoft.Extensions.Hosting;

namespace DonBot;

internal class DiscordCoreHostedService(IDiscordCore discordCore) : IHostedService
{
    private Task? _executingTask;
    private CancellationTokenSource? _stoppingCts;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _stoppingCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _executingTask = discordCore.MainAsync(_stoppingCts.Token);
        
        if (_executingTask.IsCompleted)
        {
            return _executingTask;
        }
        
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_executingTask == null)
        {
            return;
        }

        try
        {
            if (_stoppingCts != null)
            {
                _stoppingCts.Cancel();
                await Task.WhenAny(_executingTask, Task.Delay(10000, cancellationToken));
            }
        }
        finally
        {
            _stoppingCts?.Dispose();
        }
    }
}