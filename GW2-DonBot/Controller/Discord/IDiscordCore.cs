namespace DonBot.Controller.Discord;

public interface IDiscordCore
{
    public Task MainAsync(CancellationToken cancellationToken = default);
}