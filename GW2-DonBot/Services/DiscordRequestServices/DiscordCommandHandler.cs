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
    ISteamCommandService steamCommandService,
    IDeadlockCommandService deadlockCommandService)
{
    public async Task SlashCommandExecutedAsync(SocketSlashCommand command, DiscordSocketClient client)
    {
        try
        {
            switch (command.Data.Name)
            {
                case "gw2_verify": await Gw2VerifyCommandExecuted(command, client); break;
                case "gw2_deverify": await Gw2DeverifyCommandExecuted(command, client); break;
                case "gw2_points": await Gw2PointsCommandExecuted(command); break;
                case "gw2_create_raffle": await Gw2CreateRaffleCommandExecuted(command, client); break;
                case "gw2_create_event_raffle": await Gw2CreateEventRaffleCommandExecuted(command, client); break;
                case "gw2_enter_raffle": await Gw2RaffleCommandExecuted(command, client); break;
                case "gw2_enter_event_raffle": await Gw2EventRaffleCommandExecuted(command, client); break;
                case "gw2_complete_raffle": await Gw2CompleteRaffleCommandExecuted(command, client); break;
                case "gw2_complete_event_raffle": await Gw2CompleteEventRaffleCommandExecuted(command, client); break;
                case "gw2_reopen_raffle": await Gw2ReopenRaffleCommandExecuted(command, client); break;
                case "gw2_reopen_event_raffle": await Gw2ReopenEventRaffleCommandExecuted(command, client); break;
                case "gw2_start_raid": await Gw2StartRaidCommandExecuted(command, client); break;
                case "gw2_close_raid": await Gw2CloseRaidCommandExecuted(command, client); break;
                case "gw2_start_alliance_raid": await Gw2StartAllianceRaidCommandExecuted(command, client); break;
                case "gw2_server_config": await Gw2ServerConfig(command, client); break;
                case "steam_verify": await SteamVerifyCommandExecuted(command, client); break;
                case "deadlock_mmr": await DeadlockMmrCommandExecuted(command, client); break;
                case "deadlock_mmr_history": await DeadlockMmrHistoryCommandExecuted(command, client); break;
                case "deadlock_match_history": await DeadlockMatchHistoryCommandExecuted(command, client); break;
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

    private async Task Gw2VerifyCommandExecuted(SocketSlashCommand command, DiscordSocketClient client)
    {
        await command.DeferAsync(ephemeral: true);
        await verifyCommandsService.VerifyCommandExecuted(command, client);
    }

    private async Task Gw2DeverifyCommandExecuted(SocketSlashCommand command, DiscordSocketClient client)
    {
        await command.DeferAsync(ephemeral: true);
        await verifyCommandsService.DeverifyCommandExecuted(command, client);
    }

    private async Task Gw2PointsCommandExecuted(SocketSlashCommand command)
    {
        await command.DeferAsync(ephemeral: true);
        await pointsCommandsService.PointsCommandExecuted(command);
    }

    private async Task Gw2CreateRaffleCommandExecuted(SocketSlashCommand command, DiscordSocketClient client)
    {
        await command.DeferAsync(ephemeral: true);
        await raffleCommandsService.CreateRaffleCommandExecuted(command, client);
    }

    private async Task Gw2CreateEventRaffleCommandExecuted(SocketSlashCommand command, DiscordSocketClient client)
    {
        await command.DeferAsync(ephemeral: true);
        await raffleCommandsService.CreateEventRaffleCommandExecuted(command, client);
    }

    private async Task Gw2RaffleCommandExecuted(SocketSlashCommand command, DiscordSocketClient client)
    {
        await command.DeferAsync(ephemeral: true);
        await raffleCommandsService.RaffleCommandExecuted(command, client);
    }

    private async Task Gw2EventRaffleCommandExecuted(SocketSlashCommand command, DiscordSocketClient client)
    {
        await command.DeferAsync(ephemeral: true);
        await raffleCommandsService.EventRaffleCommandExecuted(command, client);
    }

    private async Task Gw2CompleteRaffleCommandExecuted(SocketSlashCommand command, DiscordSocketClient client)
    {
        await command.DeferAsync(ephemeral: true);
        await raffleCommandsService.CompleteRaffleCommandExecuted(command, client);
    }

    private async Task Gw2CompleteEventRaffleCommandExecuted(SocketSlashCommand command, DiscordSocketClient client)
    {
        await command.DeferAsync(ephemeral: true);
        await raffleCommandsService.CompleteEventRaffleCommandExecuted(command, client);
    }

    private async Task Gw2ReopenRaffleCommandExecuted(SocketSlashCommand command, DiscordSocketClient client)
    {
        await command.DeferAsync(ephemeral: true);
        await raffleCommandsService.ReopenRaffleCommandExecuted(command, client);
    }

    private async Task Gw2ReopenEventRaffleCommandExecuted(SocketSlashCommand command, DiscordSocketClient client)
    {
        await command.DeferAsync(ephemeral: true);
        await raffleCommandsService.ReopenEventRaffleCommandExecuted(command, client);
    }

    private async Task Gw2StartRaidCommandExecuted(SocketSlashCommand command, DiscordSocketClient client)
    {
        await command.DeferAsync(ephemeral: true);
        await raidCommandService.StartRaid(command, client);
    }

    private async Task Gw2CloseRaidCommandExecuted(SocketSlashCommand command, DiscordSocketClient client)
    {
        await command.DeferAsync(ephemeral: true);
        await raidCommandService.CloseRaid(command, client);
    }

    private async Task Gw2StartAllianceRaidCommandExecuted(SocketSlashCommand command, DiscordSocketClient client)
    {
        await command.DeferAsync(ephemeral: true);
        await raidCommandService.StartAllianceRaid(command, client);
    }

    private async Task Gw2ServerConfig(SocketSlashCommand command, DiscordSocketClient client)
    {
        await command.DeferAsync(ephemeral: true);
        await discordCommandService.ConfigureServer(command, client);
    }

    private async Task SteamVerifyCommandExecuted(SocketSlashCommand command, DiscordSocketClient client)
    {
        await command.DeferAsync(ephemeral: true);
        await steamCommandService.VerifySteamAccount(command, client);
    }

    private async Task DeadlockMmrCommandExecuted(SocketSlashCommand command, DiscordSocketClient client)
    {
        await command.DeferAsync(ephemeral: true);
        await deadlockCommandService.GetMmr(command, client);
    }

    private async Task DeadlockMmrHistoryCommandExecuted(SocketSlashCommand command, DiscordSocketClient client)
    {
        await command.DeferAsync(ephemeral: true);
        await deadlockCommandService.GetMmrHistory(command, client);
    }

    private async Task DeadlockMatchHistoryCommandExecuted(SocketSlashCommand command, DiscordSocketClient client)
    {
        await command.DeferAsync(ephemeral: true);
        await deadlockCommandService.GetMatchHistory(command, client);
    }

    private static async Task DefaultCommandExecuted(SocketSlashCommand command)
    {
        await command.DeferAsync(ephemeral: true);
        await command.FollowupAsync($"The command `{command.Data.Name}` is not implemented.", ephemeral: true);
    }
}
