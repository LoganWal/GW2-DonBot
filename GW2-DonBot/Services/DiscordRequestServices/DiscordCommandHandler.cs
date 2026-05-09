using Discord.WebSocket;
using Microsoft.Extensions.Logging;

namespace DonBot.Services.DiscordRequestServices;

public class DiscordCommandHandler(
    ILogger<DiscordCommandHandler> logger,
    IVerifyCommandsService verifyCommandsService,
    IPointsCommandsService pointsCommandsService,
    IRaffleCommandsService raffleCommandsService,
    IRaidCommandService raidCommandService,
    IDiscordCommandService discordCommandService,
    ILeaderboardCommandsService leaderboardCommandsService,
    IGenericCommandsService genericCommandsService)
{
    public async Task SlashCommandExecutedAsync(SocketSlashCommand command, DiscordSocketClient client)
    {
        try
        {
            switch (command.Data.Name)
            {
                case "verify": await VerifyCommandExecuted(command, client); break;
                case "deverify": await DeverifyCommandExecuted(command, client); break;
                case "points": await PointsCommandExecuted(command); break;
                case "create_raffle": await CreateRaffleCommandExecuted(command, client); break;
                case "create_event_raffle": await CreateEventRaffleCommandExecuted(command, client); break;
                case "enter_raffle": await RaffleCommandExecuted(command, client); break;
                case "enter_event_raffle": await EventRaffleCommandExecuted(command, client); break;
                case "complete_raffle": await CompleteRaffleCommandExecuted(command, client); break;
                case "complete_event_raffle": await CompleteEventRaffleCommandExecuted(command, client); break;
                case "reopen_raffle": await ReopenRaffleCommandExecuted(command, client); break;
                case "reopen_event_raffle": await ReopenEventRaffleCommandExecuted(command, client); break;
                case "start_raid": await StartRaidCommandExecuted(command, client); break;
                case "close_raid": await CloseRaidCommandExecuted(command, client); break;
                case "start_alliance_raid": await StartAllianceRaidCommandExecuted(command, client); break;
                case "my_rank": await MyRank(command); break;
                case "add_quote": await AddQuote(command); break;
                case "server_config": await ServerConfig(command); break;
                case "digut": await Digut(command); break;
                default: await DefaultCommandExecuted(command); break;
            }
        }
        catch (TimeoutException ex)
        {
            logger.LogError(ex, "Timeout occurred on {CommandDataName} command", command.Data.Name);
            await command.ModifyOriginalResponseAsync(msg => msg.Content = "Request timeout - Yell at Logan");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed {CommandDataName} command", command.Data.Name);
        }
    }

    private async Task VerifyCommandExecuted(SocketSlashCommand command, DiscordSocketClient client)
    {
        await command.DeferAsync(ephemeral: true);
        await verifyCommandsService.VerifyCommandExecuted(command, client);
    }

    private async Task DeverifyCommandExecuted(SocketSlashCommand command, DiscordSocketClient client)
    {
        await command.DeferAsync(ephemeral: true);
        await verifyCommandsService.DeverifyCommandExecuted(command, client);
    }

    private async Task PointsCommandExecuted(SocketSlashCommand command)
    {
        await command.DeferAsync(ephemeral: true);
        await pointsCommandsService.PointsCommandExecuted(command);
    }

    private async Task CreateRaffleCommandExecuted(SocketSlashCommand command, DiscordSocketClient client)
    {
        await command.DeferAsync(ephemeral: true);
        await raffleCommandsService.CreateRaffleCommandExecuted(command, client);
    }

    private async Task CreateEventRaffleCommandExecuted(SocketSlashCommand command, DiscordSocketClient client)
    {
        await command.DeferAsync(ephemeral: true);
        await raffleCommandsService.CreateEventRaffleCommandExecuted(command, client);
    }

    private async Task RaffleCommandExecuted(SocketSlashCommand command, DiscordSocketClient client)
    {
        await command.DeferAsync(ephemeral: true);
        await raffleCommandsService.RaffleCommandExecuted(command, client);
    }

    private async Task EventRaffleCommandExecuted(SocketSlashCommand command, DiscordSocketClient client)
    {
        await command.DeferAsync(ephemeral: true);
        await raffleCommandsService.EventRaffleCommandExecuted(command, client);
    }

    private async Task CompleteRaffleCommandExecuted(SocketSlashCommand command, DiscordSocketClient client)
    {
        await command.DeferAsync(ephemeral: true);
        await raffleCommandsService.CompleteRaffleCommandExecuted(command, client);
    }

    private async Task CompleteEventRaffleCommandExecuted(SocketSlashCommand command, DiscordSocketClient client)
    {
        await command.DeferAsync(ephemeral: true);
        await raffleCommandsService.CompleteEventRaffleCommandExecuted(command, client);
    }

    private async Task ReopenRaffleCommandExecuted(SocketSlashCommand command, DiscordSocketClient client)
    {
        await command.DeferAsync(ephemeral: true);
        await raffleCommandsService.ReopenRaffleCommandExecuted(command, client);
    }

    private async Task ReopenEventRaffleCommandExecuted(SocketSlashCommand command, DiscordSocketClient client)
    {
        await command.DeferAsync(ephemeral: true);
        await raffleCommandsService.ReopenEventRaffleCommandExecuted(command, client);
    }

    private async Task StartRaidCommandExecuted(SocketSlashCommand command, DiscordSocketClient client)
    {
        await command.DeferAsync(ephemeral: true);
        await raidCommandService.StartRaid(command, client);
    }

    private async Task CloseRaidCommandExecuted(SocketSlashCommand command, DiscordSocketClient client)
    {
        await command.DeferAsync(ephemeral: true);
        await raidCommandService.CloseRaid(command, client);
    }

    private async Task StartAllianceRaidCommandExecuted(SocketSlashCommand command, DiscordSocketClient client)
    {
        await command.DeferAsync(ephemeral: true);
        await raidCommandService.StartAllianceRaid(command, client);
    }

    private async Task MyRank(SocketSlashCommand command)
    {
        await command.DeferAsync(ephemeral: true);
        await leaderboardCommandsService.MyRankCommandExecuted(command);
    }

    private async Task AddQuote(SocketSlashCommand command)
    {
        await command.DeferAsync(ephemeral: true);
        await genericCommandsService.AddQuoteCommandExecuted(command);
    }

    private async Task Digut(SocketSlashCommand command)
    {
        await command.DeferAsync(ephemeral: true);
        await genericCommandsService.DigutCommandExecuted(command);
    }

    private async Task ServerConfig(SocketSlashCommand command)
    {
        await command.DeferAsync(ephemeral: true);
        await discordCommandService.ConfigureServer(command);
    }

    private static async Task DefaultCommandExecuted(SocketSlashCommand command)
    {
        await command.DeferAsync(ephemeral: true);
        await command.FollowupAsync($"The command `{command.Data.Name}` is not implemented.", ephemeral: true);
    }
}
