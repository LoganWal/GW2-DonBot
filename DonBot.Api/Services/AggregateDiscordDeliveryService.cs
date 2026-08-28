using System.Collections.Concurrent;
using Discord;
using DonBot.Core.Models;
using DonBot.Core.Models.Entities;
using DonBot.Services.GuildWarsServices;
using Microsoft.EntityFrameworkCore;

namespace DonBot.Api.Services;

public sealed record AggregateDiscordDeliveryRequest(
    long DiscordId,
    long GuildId,
    IReadOnlyList<long> FightLogIds,
    string Mode,
    long? ChannelId);

public sealed record AggregateDiscordDeliveryResult(int FightLogCount, DiscordDeliveryResult DiscordDelivery);

public enum AggregateDiscordDeliveryFailure
{
    None,
    DeliveryDisabled,
    RouteForbidden,
    InvalidRequest,
    FightNotFound,
    FightForbidden,
    FightNotReady,
    NoRenderableMessages,
    DependencyUnavailable,
    Internal
}

public sealed record AggregateDiscordDeliveryAttempt(
    AggregateDiscordDeliveryResult? Result,
    AggregateDiscordDeliveryFailure Failure)
{
    public static AggregateDiscordDeliveryAttempt Completed(AggregateDiscordDeliveryResult result) =>
        new(result, AggregateDiscordDeliveryFailure.None);

    public static AggregateDiscordDeliveryAttempt Failed(AggregateDiscordDeliveryFailure failure) =>
        new(null, failure);
}

public interface IAggregateDiscordDeliveryService
{
    Task<AggregateDiscordDeliveryAttempt> DeliverAsync(
        AggregateDiscordDeliveryRequest request,
        CancellationToken ct = default);
}

public sealed class AggregateDiscordDeliveryService(
    IDbContextFactory<DatabaseContext> dbContextFactory,
    IDiscordDeliveryGateway gateway,
    IMessageGenerationService messageGenerationService,
    ILogger<AggregateDiscordDeliveryService> logger) : IAggregateDiscordDeliveryService
{
    public const int MaxFightLogs = 100;
    private const int MaxDiscordButtonUrlLength = 512;

    public async Task<AggregateDiscordDeliveryAttempt> DeliverAsync(
        AggregateDiscordDeliveryRequest request,
        CancellationToken ct = default)
    {
        if (request.DiscordId <= 0 ||
            request.GuildId <= 0 ||
            request.FightLogIds is not { Count: >= 2 and <= MaxFightLogs } ||
            request.FightLogIds.Any(fightLogId => fightLogId <= 0) ||
            request.FightLogIds.Distinct().Count() != request.FightLogIds.Count ||
            request.Mode is not (DiscordDeliveryModes.GuildDefaults or DiscordDeliveryModes.ChannelOverride) ||
            request.Mode == DiscordDeliveryModes.GuildDefaults && request.ChannelId.HasValue ||
            request.Mode == DiscordDeliveryModes.ChannelOverride && request.ChannelId is not > 0)
        {
            return AggregateDiscordDeliveryAttempt.Failed(AggregateDiscordDeliveryFailure.InvalidRequest);
        }

        var route = await ResolveRouteAsync(request, ct);
        if (route.Failure != AggregateDiscordDeliveryFailure.None)
        {
            return AggregateDiscordDeliveryAttempt.Failed(route.Failure);
        }

        var authorization = await AuthorizeRouteAsync(request.DiscordId, request.GuildId, route.ChannelId, ct);
        if (authorization.Failure != AggregateDiscordDeliveryFailure.None)
        {
            return AggregateDiscordDeliveryAttempt.Failed(authorization.Failure);
        }

        var fightFailure = await ValidateFightsAsync(request.GuildId, request.FightLogIds, ct);
        if (fightFailure != AggregateDiscordDeliveryFailure.None)
        {
            return AggregateDiscordDeliveryAttempt.Failed(fightFailure);
        }

        List<Embed>? messages;
        string? webAppUrl;
        try
        {
            (messages, webAppUrl) = await messageGenerationService.GenerateRaidReplyReport(
                request.FightLogIds.ToList(),
                request.GuildId);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                "Aggregate Discord rendering failed for {FightCount} fights with exception type {ExceptionType}.",
                request.FightLogIds.Count,
                ex.GetType().Name);
            return AggregateDiscordDeliveryAttempt.Failed(AggregateDiscordDeliveryFailure.Internal);
        }

        if (messages is not { Count: > 0 })
        {
            return AggregateDiscordDeliveryAttempt.Failed(AggregateDiscordDeliveryFailure.NoRenderableMessages);
        }

        if (messages.Count > MaxFightLogs)
        {
            logger.LogWarning(
                "Aggregate Discord rendering produced an invalid message count of {MessageCount}.",
                messages.Count);
            return AggregateDiscordDeliveryAttempt.Failed(AggregateDiscordDeliveryFailure.Internal);
        }

        MessageComponent? finalComponents = null;
        if (!string.IsNullOrWhiteSpace(webAppUrl) && webAppUrl.Length <= MaxDiscordButtonUrlLength)
        {
            try
            {
                finalComponents = new ComponentBuilder()
                    .WithButton("View on DonBot", style: ButtonStyle.Link, url: webAppUrl)
                    .Build();
            }
            catch (Exception ex)
            {
                logger.LogWarning(
                    "Aggregate Discord component rendering failed with exception type {ExceptionType}.",
                    ex.GetType().Name);
                return AggregateDiscordDeliveryAttempt.Failed(AggregateDiscordDeliveryFailure.Internal);
            }
        }

        fightFailure = await ValidateFightsAsync(request.GuildId, request.FightLogIds, ct);
        if (fightFailure != AggregateDiscordDeliveryFailure.None)
        {
            return AggregateDiscordDeliveryAttempt.Failed(fightFailure);
        }

        route = await ResolveRouteAsync(request, ct);
        if (route.Failure != AggregateDiscordDeliveryFailure.None)
        {
            return AggregateDiscordDeliveryAttempt.Failed(route.Failure);
        }

        authorization = await AuthorizeRouteAsync(request.DiscordId, request.GuildId, route.ChannelId, ct);
        if (authorization.Failure != AggregateDiscordDeliveryFailure.None)
        {
            return AggregateDiscordDeliveryAttempt.Failed(authorization.Failure);
        }

        var sent = 0;
        var skipped = 0;
        var failed = 0;
        var ambiguous = 0;

        for (var index = 0; index < messages.Count; index++)
        {
            var components = index == messages.Count - 1 ? finalComponents : null;

            try
            {
                await gateway.SendMessageAsync(route.ChannelId, string.Empty, messages[index], components, ct);
                sent++;
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                ambiguous++;
                skipped += messages.Count - index - 1;
                break;
            }
            catch (Discord.Net.HttpException ex) when ((int)ex.HttpCode is >= 400 and < 500)
            {
                failed++;
                logger.LogWarning(
                    "Discord rejected aggregate message {MessageNumber} with status {StatusCode}.",
                    index + 1,
                    (int)ex.HttpCode);
            }
            catch (InvalidOperationException)
            {
                failed++;
            }
            catch (Exception ex)
            {
                ambiguous++;
                logger.LogWarning(
                    "Aggregate Discord message {MessageNumber} became ambiguous with exception type {ExceptionType}.",
                    index + 1,
                    ex.GetType().Name);
            }
        }

        var delivery = DiscordDeliveryResult.FromCounts(sent, skipped, failed, ambiguous);
        return AggregateDiscordDeliveryAttempt.Completed(
            new AggregateDiscordDeliveryResult(request.FightLogIds.Count, delivery));
    }

    private async Task<AggregateDiscordDeliveryFailure> ValidateFightsAsync(
        long guildId,
        IReadOnlyList<long> fightLogIds,
        CancellationToken ct)
    {
        await using var context = await dbContextFactory.CreateDbContextAsync(ct);
        var fights = await context.FightLog
            .AsNoTracking()
            .Where(fight => fightLogIds.Contains(fight.FightLogId))
            .Select(fight => new { fight.FightLogId, fight.GuildId })
            .ToDictionaryAsync(fight => fight.FightLogId, ct);

        foreach (var fightLogId in fightLogIds)
        {
            if (!fights.TryGetValue(fightLogId, out var fight))
            {
                return AggregateDiscordDeliveryFailure.FightNotFound;
            }

            if (fight.GuildId != guildId)
            {
                return AggregateDiscordDeliveryFailure.FightForbidden;
            }
        }

        var readyFightIds = await context.PlayerFightLog
            .AsNoTracking()
            .Where(player => fightLogIds.Contains(player.FightLogId))
            .Select(player => player.FightLogId)
            .Distinct()
            .ToListAsync(ct);
        var ready = readyFightIds.ToHashSet();

        return fightLogIds.All(ready.Contains)
            ? AggregateDiscordDeliveryFailure.None
            : AggregateDiscordDeliveryFailure.FightNotReady;
    }

    private async Task<(long ChannelId, AggregateDiscordDeliveryFailure Failure)> ResolveRouteAsync(
        AggregateDiscordDeliveryRequest request,
        CancellationToken ct)
    {
        await using var context = await dbContextFactory.CreateDbContextAsync(ct);
        var guild = await context.Guild
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.GuildId == request.GuildId, ct);
        if (guild is null || !guild.MannyUploaderDiscordDeliveryEnabled)
        {
            return (0, AggregateDiscordDeliveryFailure.DeliveryDisabled);
        }

        if (request.Mode == DiscordDeliveryModes.GuildDefaults)
        {
            return guild.LogReportChannelId is > 0
                ? (guild.LogReportChannelId.Value, AggregateDiscordDeliveryFailure.None)
                : (0, AggregateDiscordDeliveryFailure.RouteForbidden);
        }

        return request.Mode == DiscordDeliveryModes.ChannelOverride &&
               guild.MannyUploaderChannelOverrideEnabled &&
               request.ChannelId is > 0
            ? (request.ChannelId.Value, AggregateDiscordDeliveryFailure.None)
            : (0, AggregateDiscordDeliveryFailure.RouteForbidden);
    }

    private async Task<(bool Authorized, AggregateDiscordDeliveryFailure Failure)> AuthorizeRouteAsync(
        long discordId,
        long guildId,
        long channelId,
        CancellationToken ct)
    {
        try
        {
            var authorization = await gateway.AuthorizeChannelAsync(discordId, guildId, channelId, ct);
            return authorization.Status switch
            {
                DiscordChannelAuthorizationStatus.Authorized =>
                    (true, AggregateDiscordDeliveryFailure.None),
                DiscordChannelAuthorizationStatus.Unavailable =>
                    (false, AggregateDiscordDeliveryFailure.DependencyUnavailable),
                _ => (false, AggregateDiscordDeliveryFailure.RouteForbidden)
            };
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                "Aggregate Discord route authorization failed with exception type {ExceptionType}.",
                ex.GetType().Name);
            return (false, AggregateDiscordDeliveryFailure.DependencyUnavailable);
        }
    }
}

public interface IAggregateDeliveryAdmissionService
{
    bool TryAcquire(long discordId, long guildId, out IDisposable? lease);
}

public sealed class AggregateDeliveryAdmissionService : IAggregateDeliveryAdmissionService
{
    private const int MaxConcurrentRequests = 4;
    private const int MaxRequestsPerWindow = 5;
    private static readonly TimeSpan WindowLength = TimeSpan.FromMinutes(1);

    private readonly SemaphoreSlim _concurrency = new(MaxConcurrentRequests, MaxConcurrentRequests);
    private readonly ConcurrentDictionary<(long DiscordId, long GuildId), RateWindow> _windows = new();

    public bool TryAcquire(long discordId, long guildId, out IDisposable? lease)
    {
        lease = null;
        if (!_concurrency.Wait(0))
        {
            return false;
        }

        var now = DateTime.UtcNow;
        var window = _windows.GetOrAdd((discordId, guildId), _ => new RateWindow(now));
        lock (window)
        {
            if (now - window.StartedAt >= WindowLength)
            {
                window.StartedAt = now;
                window.RequestCount = 0;
            }

            if (window.RequestCount >= MaxRequestsPerWindow)
            {
                _concurrency.Release();
                return false;
            }

            window.RequestCount++;
        }

        if (_windows.Count > 1024)
        {
            foreach (var item in _windows.Where(item => now - item.Value.StartedAt >= WindowLength * 2))
            {
                _windows.TryRemove(item.Key, out _);
            }
        }

        lease = new AdmissionLease(_concurrency);
        return true;
    }

    private sealed class RateWindow(DateTime startedAt)
    {
        public DateTime StartedAt { get; set; } = startedAt;

        public int RequestCount { get; set; }
    }

    private sealed class AdmissionLease(SemaphoreSlim concurrency) : IDisposable
    {
        private int _disposed;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0)
            {
                concurrency.Release();
            }
        }
    }
}
