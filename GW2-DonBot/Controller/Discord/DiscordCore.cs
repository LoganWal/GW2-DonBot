using Discord;
using Discord.WebSocket;
using DonBot.Services.LoggingServices;
using DonBot.Services.SchedulerServices;
using DonBot.Services.DiscordRequestServices;
using DonBot.Services.DiscordServices;
using DonBot.Services.SecretsServices;
using Microsoft.Extensions.Logging;
using ConnectionState = Discord.ConnectionState;

namespace DonBot.Controller.Discord;

public sealed class DiscordCore(
    ILogger<DiscordCore> logger,
    ISecretService secretService,
    IPollingTasksService pollingTasksService,
    ILoggingService loggingService,
    SchedulerService schedulerService,
    DiscordSocketClient client,
    DiscordCommandRegistrar commandRegistrar,
    DiscordCommandHandler commandHandler,
    DiscordButtonHandler buttonHandler,
    DiscordMessageHandler messageHandler)
    : IDiscordCore
{
    private CancellationTokenSource? _pollingRolesCancellationTokenSource;
    private CancellationTokenSource? _scheduleServiceCancellationTokenSource;
    private Task? _pollingRolesTask;

    public async Task MainAsync(CancellationToken cancellationToken = default)
    {
        await client.LoginAsync(TokenType.Bot, secretService.FetchDonBotToken());
        await client.StartAsync();

        logger.LogInformation("GW2-DonBot attempting to connect...");

        await WaitForConnectionAsync(cancellationToken);
        await commandRegistrar.RegisterCommands(client);

        logger.LogInformation("GW2-DonBot connected.");

        await messageHandler.LoadExistingFightLogs();

        RegisterEventHandlers();

        _pollingRolesCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _pollingRolesTask = PollingRolesTask(TimeSpan.FromMinutes(30), _pollingRolesCancellationTokenSource.Token);

        _scheduleServiceCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        await schedulerService.StartAsync(_scheduleServiceCancellationTokenSource.Token);

        logger.LogInformation("GW2-DonBot setup - ready to cause chaos");

        try
        {
            await Task.Delay(Timeout.Infinite, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            logger.LogInformation("Service shutdown initiated");
        }
        finally
        {
            await CleanupAsync();
        }
    }

    private void RegisterEventHandlers()
    {
        client.Log += loggingService.LogAsync;
        client.MessageReceived += messageHandler.MessageReceivedAsync;
        client.SlashCommandExecuted += command => commandHandler.SlashCommandExecutedAsync(command, client);
        client.ButtonExecuted += buttonHandler.ButtonExecutedAsync;
    }

    private void UnregisterEventHandlers()
    {
        client.Log -= loggingService.LogAsync;
        client.MessageReceived -= messageHandler.MessageReceivedAsync;
    }

    private async Task WaitForConnectionAsync(CancellationToken cancellationToken = default)
    {
        while (client.ConnectionState != ConnectionState.Connected)
        {
            await Task.Delay(100, cancellationToken);
        }
    }

    private async Task PollingRolesTask(TimeSpan interval, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await pollingTasksService.PollingRoles(client);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error occurred while polling roles");
            }
            await Task.Delay(interval, cancellationToken);
        }
    }

    private async Task CleanupAsync()
    {
        logger.LogInformation("Shutting down Discord client and services...");

        if (_pollingRolesCancellationTokenSource is not null)
        {
            await _pollingRolesCancellationTokenSource.CancelAsync();
        }

        if (_scheduleServiceCancellationTokenSource is not null)
        {
            await _scheduleServiceCancellationTokenSource.CancelAsync();
        }

        if (_pollingRolesTask is not null)
        {
            try
            {
                await _pollingRolesTask.WaitAsync(TimeSpan.FromSeconds(5));
            }
            catch (TimeoutException)
            {
                logger.LogWarning("Polling task did not complete within timeout period");
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Error waiting for polling task to complete");
            }
        }

        UnregisterEventHandlers();

        await client.StopAsync();

        _pollingRolesCancellationTokenSource?.Dispose();
        _scheduleServiceCancellationTokenSource?.Dispose();

        logger.LogInformation("Discord client shutdown completed");
    }
}
