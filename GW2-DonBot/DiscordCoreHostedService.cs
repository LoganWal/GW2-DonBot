using DonBot.Controller.Discord;
using Microsoft.Extensions.Hosting;

namespace DonBot
{
    internal class DiscordCoreHostedService(IDiscordCore discordCore) : IHostedService
    {
        public async Task StartAsync(CancellationToken cancellationToken)
        {
            await discordCore.MainAsync();
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }
}