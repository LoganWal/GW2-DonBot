using Discord;
using Discord.Net;
using Discord.WebSocket;
using DonBot.Core.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DonBot.Services.DiscordServices;

public interface IScheduledMessageDeleteScheduler
{
    Task ScheduleDeleteAsync(ulong channelId, ulong messageId, TimeSpan delay, string reason);
}

public interface IScheduledMessageDeleteClient
{
    Task DeleteMessageAsync(ulong channelId, ulong messageId);
}

public sealed class DiscordScheduledMessageDeleteClient(DiscordSocketClient client) : IScheduledMessageDeleteClient
{
    public async Task DeleteMessageAsync(ulong channelId, ulong messageId)
    {
        IMessageChannel? channel = client.GetChannel(channelId) as IMessageChannel;
        if (channel is null)
        {
            var restChannel = await client.GetChannelAsync(channelId);
            channel = restChannel as IMessageChannel;
        }

        if (channel is null)
        {
            throw new InvalidOperationException($"Unable to resolve Discord message channel {channelId}.");
        }

        await channel.DeleteMessageAsync(messageId);
    }
}

public sealed class ScheduledMessageDeleteService(
    IDbContextFactory<DatabaseContext> contextFactory,
    IScheduledMessageDeleteClient deleteClient,
    ILogger<ScheduledMessageDeleteService> logger)
    : BackgroundService, IScheduledMessageDeleteScheduler
{
    private const int DueDeleteBatchSize = 100;
    private const int UnknownChannelDiscordCode = 10003;
    private const int UnknownMessageDiscordCode = 10008;
    private const int MissingAccessDiscordCode = 50001;
    private const int MissingPermissionsDiscordCode = 50013;
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan FailedDeleteRetryDelay = TimeSpan.FromMinutes(5);

    public async Task ScheduleDeleteAsync(ulong channelId, ulong messageId, TimeSpan delay, string reason)
    {
        var now = DateTime.UtcNow;

        await using var context = await contextFactory.CreateDbContextAsync();
        await context.ScheduledMessageDelete.AddAsync(new ScheduledMessageDelete
        {
            ChannelId = (long)channelId,
            MessageId = (long)messageId,
            CreatedUtc = now,
            DeleteAfterUtc = now.Add(delay),
            Reason = reason
        });
        await context.SaveChangesAsync();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("ScheduledMessageDeleteService is starting.");

        try
        {
            await ProcessDueDeletesSafelyAsync(stoppingToken);

            using var timer = new PeriodicTimer(PollInterval);
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                await ProcessDueDeletesSafelyAsync(stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            logger.LogInformation("ScheduledMessageDeleteService is stopping.");
        }
    }

    private async Task ProcessDueDeletesSafelyAsync(CancellationToken stoppingToken)
    {
        try
        {
            await ProcessDueDeletesAsync(stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error processing scheduled message deletes.");
        }
    }

    internal async Task<int> ProcessDueDeletesAsync(CancellationToken stoppingToken = default)
    {
        var now = DateTime.UtcNow;
        List<ScheduledMessageDelete> dueDeletes;
        await using (var context = await contextFactory.CreateDbContextAsync(stoppingToken))
        {
            dueDeletes = await context.ScheduledMessageDelete
                .Where(smd => smd.DeleteAfterUtc <= now)
                .OrderBy(smd => smd.DeleteAfterUtc)
                .Take(DueDeleteBatchSize)
                .ToListAsync(stoppingToken);
        }

        foreach (var scheduledDelete in dueDeletes)
        {
            if (await TryDeleteMessageAsync(scheduledDelete, stoppingToken))
            {
                await RemoveScheduledDeleteAsync(scheduledDelete.ScheduledMessageDeleteId, stoppingToken);
            }
        }

        return dueDeletes.Count;
    }

    private async Task<bool> TryDeleteMessageAsync(ScheduledMessageDelete scheduledDelete, CancellationToken stoppingToken)
    {
        stoppingToken.ThrowIfCancellationRequested();

        try
        {
            await deleteClient.DeleteMessageAsync((ulong)scheduledDelete.ChannelId, (ulong)scheduledDelete.MessageId);
            logger.LogInformation("Deleted scheduled message {MessageId} in channel {ChannelId}.",
                scheduledDelete.MessageId, scheduledDelete.ChannelId);
            return true;
        }
        catch (HttpException ex) when (IsMissingDiscordResource(ex))
        {
            logger.LogInformation("Scheduled message {MessageId} in channel {ChannelId} no longer exists. Removing record.",
                scheduledDelete.MessageId, scheduledDelete.ChannelId);
            return true;
        }
        catch (HttpException ex) when (IsTerminalDeleteFailure(ex))
        {
            logger.LogWarning(ex, "Cannot delete scheduled message {MessageId} in channel {ChannelId}. Removing record.",
                scheduledDelete.MessageId, scheduledDelete.ChannelId);
            return true;
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to delete scheduled message {MessageId} in channel {ChannelId}. Will retry.",
                scheduledDelete.MessageId, scheduledDelete.ChannelId);
            await DelayScheduledDeleteRetryAsync(scheduledDelete.ScheduledMessageDeleteId, stoppingToken);
            return false;
        }
    }

    private async Task DelayScheduledDeleteRetryAsync(long scheduledMessageDeleteId, CancellationToken stoppingToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(stoppingToken);
        var scheduledDelete = await context.ScheduledMessageDelete
            .FirstOrDefaultAsync(smd => smd.ScheduledMessageDeleteId == scheduledMessageDeleteId, stoppingToken);
        if (scheduledDelete is null)
        {
            return;
        }

        scheduledDelete.DeleteAfterUtc = DateTime.UtcNow.Add(FailedDeleteRetryDelay);
        context.ScheduledMessageDelete.Update(scheduledDelete);
        await context.SaveChangesAsync(stoppingToken);
    }

    private async Task RemoveScheduledDeleteAsync(long scheduledMessageDeleteId, CancellationToken stoppingToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(stoppingToken);
        context.ScheduledMessageDelete.Remove(new ScheduledMessageDelete
        {
            ScheduledMessageDeleteId = scheduledMessageDeleteId
        });
        await context.SaveChangesAsync(stoppingToken);
    }

    private static bool IsMissingDiscordResource(HttpException ex) =>
        ex.DiscordCode.HasValue
        && ((int)ex.DiscordCode.Value == UnknownChannelDiscordCode
            || (int)ex.DiscordCode.Value == UnknownMessageDiscordCode);

    private static bool IsTerminalDeleteFailure(HttpException ex) =>
        ex.DiscordCode.HasValue
        && ((int)ex.DiscordCode.Value == MissingAccessDiscordCode
            || (int)ex.DiscordCode.Value == MissingPermissionsDiscordCode);
}
