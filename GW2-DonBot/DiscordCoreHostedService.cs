using Controller.Discord;
using Microsoft.Extensions.Hosting;

internal class DiscordCoreHostedService : IHostedService
{
    private readonly IDiscordCore _discordCore;

    public DiscordCoreHostedService(IDiscordCore discordCore)
    {
        _discordCore = discordCore;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await _discordCore.MainAsync();
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}