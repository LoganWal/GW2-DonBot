using Discord;
using DonBot.Core.Models;
using DonBot.Core.Models.Entities;
using DonBot.Core.Models.Enums;
using DonBot.Core.Models.GuildWars2;
using DonBot.Models.Statics;
using DonBot.Services.GuildWarsServices.MessageGeneration;
using Microsoft.EntityFrameworkCore;

namespace DonBot.Api.Services;

public sealed record DiscordDeliveryCapabilities(
    bool Enabled,
    bool DefaultsAvailable,
    bool ChannelOverrideAllowed,
    IReadOnlyList<string> EnabledMessageKinds,
    IReadOnlyList<DiscordAuthorizedChannel> Channels);

public sealed record DiscordDeliveryValidationResult(bool Accepted, string? ErrorCode)
{
    public static DiscordDeliveryValidationResult Success() => new(true, null);

    public static DiscordDeliveryValidationResult Failed(string errorCode) => new(false, errorCode);
}

public sealed record DiscordDeliveryResult(
    bool Requested,
    string Outcome,
    int Sent,
    int Skipped,
    int Failed,
    int Ambiguous)
{
    public static DiscordDeliveryResult NotRequested { get; } = new(false, "not_requested", 0, 0, 0, 0);

    public static DiscordDeliveryResult FromReceipts(IReadOnlyCollection<LogUploadDiscordDeliveryReceipt> receipts)
    {
        var sent = receipts.Count(receipt => receipt.Status == DiscordDeliveryReceiptStatuses.Sent);
        var skipped = receipts.Count(receipt => receipt.Status == DiscordDeliveryReceiptStatuses.Skipped);
        var failed = receipts.Count(receipt => receipt.Status == DiscordDeliveryReceiptStatuses.Failed);
        var ambiguous = receipts.Count(receipt => receipt.Status == DiscordDeliveryReceiptStatuses.Ambiguous);
        var total = sent + skipped + failed + ambiguous;
        var outcome = total == 0 || skipped == total
            ? "skipped"
            : sent == total
                ? "sent"
                : failed == total
                    ? "failed"
                    : ambiguous == total
                        ? "ambiguous"
                        : "partial";

        return new DiscordDeliveryResult(true, outcome, sent, skipped, failed, ambiguous);
    }
}

public interface IDiscordUploadDeliveryService
{
    Task<DiscordDeliveryCapabilities> GetCapabilitiesAsync(
        Guild guild,
        long discordId,
        CancellationToken ct = default);

    Task<DiscordDeliveryValidationResult> ValidateAsync(
        long discordId,
        long guildId,
        string mode,
        long? channelId,
        CancellationToken ct = default);

    Task<DiscordDeliveryResult> DeliverAsync(
        LogUpload upload,
        EliteInsightDataModel data,
        CancellationToken ct = default);

    Task<DiscordDeliveryResult> GetResultAsync(long uploadId, CancellationToken ct = default);

    Task<DiscordDeliveryResult> RecordFailureAsync(
        long uploadId,
        string failureCode,
        CancellationToken ct = default);

    Task NormalizeInterruptedAsync(CancellationToken ct = default);
}

public sealed class DiscordUploadDeliveryService(
    IDbContextFactory<DatabaseContext> dbContextFactory,
    IDiscordDeliveryGateway gateway,
    IPvEFightSummaryService pveRenderer,
    IWvWFightSummaryService wvwRenderer,
    ILogger<DiscordUploadDeliveryService> logger) : IDiscordUploadDeliveryService
{
    private const int MaxChannelsPerGuild = 256;

    public async Task<DiscordDeliveryCapabilities> GetCapabilitiesAsync(
        Guild guild,
        long discordId,
        CancellationToken ct = default)
    {
        var messageKinds = EnabledMessageKinds(guild);
        if (!guild.MannyUploaderDiscordDeliveryEnabled)
        {
            return new DiscordDeliveryCapabilities(false, false, false, [], []);
        }

        var defaultChannelIds = DefaultChannelIds(guild).Distinct().ToList();
        var defaultsAvailable = false;
        foreach (var channelId in defaultChannelIds)
        {
            if (await IsAuthorizedChannelSafeAsync(discordId, guild.GuildId, channelId, ct))
            {
                defaultsAvailable = true;
                break;
            }
        }

        IReadOnlyList<DiscordAuthorizedChannel> channels = [];
        if (guild.MannyUploaderChannelOverrideEnabled)
        {
            channels = (await GetAuthorizedChannelsSafeAsync(discordId, guild.GuildId, ct))
                .DistinctBy(channel => channel.ChannelId)
                .Take(MaxChannelsPerGuild)
                .ToList();
        }

        return new DiscordDeliveryCapabilities(
            true,
            defaultsAvailable,
            guild.MannyUploaderChannelOverrideEnabled,
            messageKinds,
            channels);
    }

    public async Task<DiscordDeliveryValidationResult> ValidateAsync(
        long discordId,
        long guildId,
        string mode,
        long? channelId,
        CancellationToken ct = default)
    {
        await using var context = await dbContextFactory.CreateDbContextAsync(ct);
        var guild = await context.Guild.AsNoTracking().FirstOrDefaultAsync(item => item.GuildId == guildId, ct);
        if (guild is null || !guild.MannyUploaderDiscordDeliveryEnabled)
        {
            return DiscordDeliveryValidationResult.Failed("discord_delivery_disabled");
        }

        if (EnabledMessageKinds(guild).Count == 0)
        {
            return DiscordDeliveryValidationResult.Failed("discord_delivery_unavailable");
        }

        if (mode == DiscordDeliveryModes.GuildDefaults)
        {
            if (channelId.HasValue)
            {
                return DiscordDeliveryValidationResult.Failed("invalid_discord_delivery");
            }

            foreach (var defaultChannelId in DefaultChannelIds(guild).Distinct())
            {
                if (await IsAuthorizedChannelSafeAsync(discordId, guildId, defaultChannelId, ct))
                {
                    return DiscordDeliveryValidationResult.Success();
                }
            }

            return DiscordDeliveryValidationResult.Failed("discord_delivery_unavailable");
        }

        if (mode != DiscordDeliveryModes.ChannelOverride ||
            !guild.MannyUploaderChannelOverrideEnabled ||
            channelId is not > 0)
        {
            return DiscordDeliveryValidationResult.Failed("invalid_discord_delivery");
        }

        return await IsAuthorizedChannelSafeAsync(discordId, guildId, channelId.Value, ct)
            ? DiscordDeliveryValidationResult.Success()
            : DiscordDeliveryValidationResult.Failed("discord_channel_forbidden");
    }

    public async Task<DiscordDeliveryResult> DeliverAsync(
        LogUpload upload,
        EliteInsightDataModel data,
        CancellationToken ct = default)
    {
        if (upload.DiscordDeliveryMode is null)
        {
            return DiscordDeliveryResult.NotRequested;
        }

        await using var context = await dbContextFactory.CreateDbContextAsync(ct);
        var guild = await context.Guild.AsNoTracking().FirstOrDefaultAsync(item => item.GuildId == upload.GuildId, ct);
        var fightLog = upload.FightLogId is > 0
            ? await context.FightLog.AsNoTracking().FirstOrDefaultAsync(item => item.FightLogId == upload.FightLogId, ct)
            : null;
        if (guild is null || fightLog is null)
        {
            return await RecordUnavailableAsync(upload, data, "delivery_context_missing", ct);
        }

        IReadOnlyList<DeliveryMessage> messages;
        try
        {
            messages = await BuildMessagesAsync(data, guild, fightLog);
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                "Discord rendering failed for upload {UploadId} with exception type {ExceptionType}.",
                upload.LogUploadId,
                ex.GetType().Name);
            return await RecordFailureAsync(upload, data, "summary_render_failed", ct);
        }
        foreach (var message in messages)
        {
            var configuredChannelId = ResolveChannelId(upload, guild, message.Kind);
            var receipt = await GetOrCreateReceiptAsync(
                upload.LogUploadId,
                message.Kind,
                configuredChannelId,
                ct);
            if (IsTerminal(receipt.Status))
            {
                continue;
            }

            var channelId = receipt.ResolvedChannelId;
            if (!guild.MannyUploaderDiscordDeliveryEnabled || channelId is not > 0)
            {
                await SetReceiptAsync(receipt.LogUploadDiscordDeliveryReceiptId, DiscordDeliveryReceiptStatuses.Skipped, null, "destination_unavailable", ct);
                continue;
            }

            if (!await IsRouteStillEnabledAsync(upload, message.Kind, channelId.Value, ct))
            {
                await SetReceiptAsync(receipt.LogUploadDiscordDeliveryReceiptId, DiscordDeliveryReceiptStatuses.Skipped, null, "route_revoked", ct);
                continue;
            }

            if (!await IsAuthorizedChannelSafeAsync(upload.DiscordId, upload.GuildId, channelId.Value, ct))
            {
                await SetReceiptAsync(receipt.LogUploadDiscordDeliveryReceiptId, DiscordDeliveryReceiptStatuses.Skipped, null, "authorization_revoked", ct);
                continue;
            }

            var claimed = await ClaimReceiptAsync(receipt.LogUploadDiscordDeliveryReceiptId, ct);
            if (!claimed)
            {
                continue;
            }

            try
            {
                var messageId = await gateway.SendMessageAsync(
                    channelId.Value,
                    message.Text,
                    message.Embed,
                    message.Components,
                    ct);
                await SetReceiptAsync(receipt.LogUploadDiscordDeliveryReceiptId, DiscordDeliveryReceiptStatuses.Sent, messageId, null, ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Discord.Net.HttpException ex) when ((int)ex.HttpCode is >= 400 and < 500)
            {
                logger.LogWarning(
                    "Discord rejected delivery for upload {UploadId}, kind {MessageKind}, with status {StatusCode}.",
                    upload.LogUploadId,
                    message.Kind,
                    (int)ex.HttpCode);
                await SetReceiptAsync(receipt.LogUploadDiscordDeliveryReceiptId, DiscordDeliveryReceiptStatuses.Failed, null, "discord_request_rejected", ct);
            }
            catch (InvalidOperationException)
            {
                await SetReceiptAsync(receipt.LogUploadDiscordDeliveryReceiptId, DiscordDeliveryReceiptStatuses.Failed, null, "discord_destination_unavailable", ct);
            }
            catch (Exception ex)
            {
                logger.LogWarning(
                    "Discord delivery became ambiguous for upload {UploadId}, kind {MessageKind}, with exception type {ExceptionType}.",
                    upload.LogUploadId,
                    message.Kind,
                    ex.GetType().Name);
                await SetReceiptAsync(receipt.LogUploadDiscordDeliveryReceiptId, DiscordDeliveryReceiptStatuses.Ambiguous, null, "discord_send_ambiguous", ct);
            }
        }

        return await GetResultAsync(upload.LogUploadId, ct);
    }

    public async Task<DiscordDeliveryResult> GetResultAsync(long uploadId, CancellationToken ct = default)
    {
        await using var context = await dbContextFactory.CreateDbContextAsync(ct);
        var requested = await context.LogUpload.AsNoTracking()
            .Where(upload => upload.LogUploadId == uploadId)
            .Select(upload => upload.DiscordDeliveryMode != null)
            .FirstOrDefaultAsync(ct);
        if (!requested)
        {
            return DiscordDeliveryResult.NotRequested;
        }

        var receipts = await context.LogUploadDiscordDeliveryReceipt.AsNoTracking()
            .Where(receipt => receipt.LogUploadId == uploadId)
            .ToListAsync(ct);
        return DiscordDeliveryResult.FromReceipts(receipts);
    }

    public async Task<DiscordDeliveryResult> RecordFailureAsync(
        long uploadId,
        string failureCode,
        CancellationToken ct = default)
    {
        await using var context = await dbContextFactory.CreateDbContextAsync(ct);
        var uploadState = await context.LogUpload.AsNoTracking()
            .FirstOrDefaultAsync(item => item.LogUploadId == uploadId, ct);
        if (uploadState?.DiscordDeliveryMode is null)
        {
            return DiscordDeliveryResult.NotRequested;
        }

        var updated = await context.LogUploadDiscordDeliveryReceipt
            .Where(receipt => receipt.LogUploadId == uploadId &&
                receipt.Status == DiscordDeliveryReceiptStatuses.Pending)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(receipt => receipt.Status, DiscordDeliveryReceiptStatuses.Failed)
                    .SetProperty(receipt => receipt.FailureCode, failureCode)
                    .SetProperty(receipt => receipt.UpdatedAt, DateTime.UtcNow),
                ct);

        var hasReceipts = updated > 0 || await context.LogUploadDiscordDeliveryReceipt.AsNoTracking()
            .AnyAsync(receipt => receipt.LogUploadId == uploadId, ct);
        if (!hasReceipts)
        {
            var fightType = uploadState.FightLogId is > 0
                ? await context.FightLog.AsNoTracking()
                    .Where(fight => fight.FightLogId == uploadState.FightLogId)
                    .Select(fight => (short?)fight.FightType)
                    .FirstOrDefaultAsync(ct)
                : null;
            var messageKind = fightType == (short)FightTypesEnum.WvW
                ? DiscordDeliveryMessageKinds.WvwSummary
                : DiscordDeliveryMessageKinds.PveSummary;
            var receipt = await GetOrCreateReceiptAsync(uploadId, messageKind, null, ct);
            if (!IsTerminal(receipt.Status))
            {
                await SetReceiptAsync(
                    receipt.LogUploadDiscordDeliveryReceiptId,
                    DiscordDeliveryReceiptStatuses.Failed,
                    null,
                    failureCode,
                    ct);
            }
        }

        return await GetResultAsync(uploadId, ct);
    }

    public async Task NormalizeInterruptedAsync(CancellationToken ct = default)
    {
        await using var context = await dbContextFactory.CreateDbContextAsync(ct);
        await context.LogUploadDiscordDeliveryReceipt
            .Where(receipt => receipt.Status == DiscordDeliveryReceiptStatuses.Sending)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(receipt => receipt.Status, DiscordDeliveryReceiptStatuses.Ambiguous)
                    .SetProperty(receipt => receipt.FailureCode, "send_interrupted")
                    .SetProperty(receipt => receipt.UpdatedAt, DateTime.UtcNow),
                ct);
    }

    private async Task<IReadOnlyList<DeliveryMessage>> BuildMessagesAsync(
        EliteInsightDataModel data,
        Guild guild,
        FightLog fightLog)
    {
        if (!data.FightEliteInsightDataModel.Wvw)
        {
            var rendered = await pveRenderer.RenderSimple(data, guild.GuildId, fightLog);
            var components = rendered.WebAppUrl is null
                ? null
                : new ComponentBuilder().WithButton("View on DonBot", style: ButtonStyle.Link, url: rendered.WebAppUrl).Build();
            return [new DeliveryMessage(DiscordDeliveryMessageKinds.PveSummary, string.Empty, rendered.Embed, components)];
        }

        var standard = await wvwRenderer.Render(data, false, guild, fightLog);
        var standardComponents = new ComponentBuilder().WithButton("Know My Enemy", ButtonId.KnowMyEnemy);
        if (standard.WebAppUrl is not null)
        {
            standardComponents.WithButton("View on DonBot", style: ButtonStyle.Link, url: standard.WebAppUrl);
        }

        var messages = new List<DeliveryMessage>
        {
            new(DiscordDeliveryMessageKinds.WvwSummary, string.Empty, standard.Embed, standardComponents.Build())
        };
        if (guild.AdvanceLogReportChannelId.HasValue)
        {
            var advanced = await wvwRenderer.Render(data, true, guild, fightLog: null);
            messages.Add(new DeliveryMessage(DiscordDeliveryMessageKinds.WvwAdvanced, string.Empty, advanced.Embed, null));
        }
        if (guild.StreamLogChannelId.HasValue)
        {
            messages.Add(new DeliveryMessage(DiscordDeliveryMessageKinds.WvwStream, standard.StreamMessage, null, null));
        }

        return messages;
    }

    private async Task<DiscordDeliveryResult> RecordUnavailableAsync(
        LogUpload upload,
        EliteInsightDataModel data,
        string failureCode,
        CancellationToken ct)
    {
        var kind = data.FightEliteInsightDataModel.Wvw
            ? DiscordDeliveryMessageKinds.WvwSummary
            : DiscordDeliveryMessageKinds.PveSummary;
        var receipt = await GetOrCreateReceiptAsync(upload.LogUploadId, kind, null, ct);
        if (!IsTerminal(receipt.Status))
        {
            await SetReceiptAsync(receipt.LogUploadDiscordDeliveryReceiptId, DiscordDeliveryReceiptStatuses.Skipped, null, failureCode, ct);
        }

        return await GetResultAsync(upload.LogUploadId, ct);
    }

    private async Task<DiscordDeliveryResult> RecordFailureAsync(
        LogUpload upload,
        EliteInsightDataModel data,
        string failureCode,
        CancellationToken ct)
    {
        var kind = data.FightEliteInsightDataModel.Wvw
            ? DiscordDeliveryMessageKinds.WvwSummary
            : DiscordDeliveryMessageKinds.PveSummary;
        var receipt = await GetOrCreateReceiptAsync(upload.LogUploadId, kind, null, ct);
        if (!IsTerminal(receipt.Status))
        {
            await SetReceiptAsync(receipt.LogUploadDiscordDeliveryReceiptId, DiscordDeliveryReceiptStatuses.Failed, null, failureCode, ct);
        }

        return await GetResultAsync(upload.LogUploadId, ct);
    }

    private async Task<LogUploadDiscordDeliveryReceipt> GetOrCreateReceiptAsync(
        long uploadId,
        string messageKind,
        long? channelId,
        CancellationToken ct)
    {
        await using var context = await dbContextFactory.CreateDbContextAsync(ct);
        var existing = await context.LogUploadDiscordDeliveryReceipt.AsNoTracking()
            .FirstOrDefaultAsync(
                receipt => receipt.LogUploadId == uploadId && receipt.MessageKind == messageKind,
                ct);
        if (existing is not null)
        {
            return existing;
        }

        var now = DateTime.UtcNow;
        var receipt = new LogUploadDiscordDeliveryReceipt
        {
            LogUploadId = uploadId,
            MessageKind = messageKind,
            ResolvedChannelId = channelId,
            CreatedAt = now,
            UpdatedAt = now
        };
        context.LogUploadDiscordDeliveryReceipt.Add(receipt);
        try
        {
            await context.SaveChangesAsync(ct);
            return receipt;
        }
        catch (DbUpdateException)
        {
            context.ChangeTracker.Clear();
            return await context.LogUploadDiscordDeliveryReceipt.AsNoTracking()
                .SingleAsync(
                    item => item.LogUploadId == uploadId && item.MessageKind == messageKind,
                    ct);
        }
    }

    private async Task<bool> ClaimReceiptAsync(long receiptId, CancellationToken ct)
    {
        await using var context = await dbContextFactory.CreateDbContextAsync(ct);
        var updated = await context.LogUploadDiscordDeliveryReceipt
            .Where(receipt => receipt.LogUploadDiscordDeliveryReceiptId == receiptId &&
                receipt.Status == DiscordDeliveryReceiptStatuses.Pending)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(receipt => receipt.Status, DiscordDeliveryReceiptStatuses.Sending)
                    .SetProperty(receipt => receipt.UpdatedAt, DateTime.UtcNow),
                ct);
        return updated == 1;
    }

    private async Task SetReceiptAsync(
        long receiptId,
        string status,
        long? messageId,
        string? failureCode,
        CancellationToken ct)
    {
        await using var context = await dbContextFactory.CreateDbContextAsync(ct);
        await context.LogUploadDiscordDeliveryReceipt
            .Where(receipt => receipt.LogUploadDiscordDeliveryReceiptId == receiptId)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(receipt => receipt.Status, status)
                    .SetProperty(receipt => receipt.DiscordMessageId, messageId)
                    .SetProperty(receipt => receipt.FailureCode, failureCode)
                    .SetProperty(receipt => receipt.UpdatedAt, DateTime.UtcNow),
                ct);
    }

    private static IReadOnlyList<string> EnabledMessageKinds(Guild guild)
    {
        var kinds = new List<string>();
        if (guild.LogReportChannelId.HasValue)
        {
            kinds.Add(DiscordDeliveryMessageKinds.PveSummary);
            kinds.Add(DiscordDeliveryMessageKinds.WvwSummary);
        }
        if (guild.AdvanceLogReportChannelId.HasValue)
        {
            kinds.Add(DiscordDeliveryMessageKinds.WvwAdvanced);
        }
        if (guild.StreamLogChannelId.HasValue)
        {
            kinds.Add(DiscordDeliveryMessageKinds.WvwStream);
        }

        return kinds;
    }

    private static IEnumerable<long> DefaultChannelIds(Guild guild)
    {
        if (guild.LogReportChannelId is { } primary)
        {
            yield return primary;
        }
        if (guild.AdvanceLogReportChannelId is { } advanced)
        {
            yield return advanced;
        }
        if (guild.StreamLogChannelId is { } stream)
        {
            yield return stream;
        }
    }

    private static long? ResolveChannelId(LogUpload upload, Guild guild, string messageKind)
    {
        if (upload.DiscordDeliveryMode == DiscordDeliveryModes.ChannelOverride)
        {
            return upload.DiscordDeliveryChannelId;
        }

        return messageKind switch
        {
            DiscordDeliveryMessageKinds.PveSummary or DiscordDeliveryMessageKinds.WvwSummary => guild.LogReportChannelId,
            DiscordDeliveryMessageKinds.WvwAdvanced => guild.AdvanceLogReportChannelId,
            DiscordDeliveryMessageKinds.WvwStream => guild.StreamLogChannelId,
            _ => null
        };
    }

    private async Task<bool> IsRouteStillEnabledAsync(
        LogUpload upload,
        string messageKind,
        long channelId,
        CancellationToken ct)
    {
        await using var context = await dbContextFactory.CreateDbContextAsync(ct);
        var guild = await context.Guild.AsNoTracking()
            .FirstOrDefaultAsync(item => item.GuildId == upload.GuildId, ct);
        if (guild is null || !guild.MannyUploaderDiscordDeliveryEnabled)
        {
            return false;
        }

        if (upload.DiscordDeliveryMode == DiscordDeliveryModes.ChannelOverride)
        {
            return guild.MannyUploaderChannelOverrideEnabled &&
                upload.DiscordDeliveryChannelId == channelId &&
                EnabledMessageKinds(guild).Contains(messageKind);
        }

        return ResolveChannelId(upload, guild, messageKind) == channelId;
    }

    private async Task<IReadOnlyList<DiscordAuthorizedChannel>> GetAuthorizedChannelsSafeAsync(
        long discordId,
        long guildId,
        CancellationToken ct)
    {
        try
        {
            return await gateway.GetAuthorizedChannelsAsync(discordId, guildId, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                "Discord channel discovery failed for user {DiscordId} in guild {GuildId} with exception type {ExceptionType}.",
                discordId,
                guildId,
                ex.GetType().Name);
            return [];
        }
    }

    private async Task<bool> IsAuthorizedChannelSafeAsync(
        long discordId,
        long guildId,
        long channelId,
        CancellationToken ct)
    {
        try
        {
            return await gateway.IsAuthorizedChannelAsync(discordId, guildId, channelId, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                "Discord channel authorization failed for user {DiscordId} in guild {GuildId} with exception type {ExceptionType}.",
                discordId,
                guildId,
                ex.GetType().Name);
            return false;
        }
    }

    private static bool IsTerminal(string status) => status is
        DiscordDeliveryReceiptStatuses.Sent or
        DiscordDeliveryReceiptStatuses.Skipped or
        DiscordDeliveryReceiptStatuses.Failed or
        DiscordDeliveryReceiptStatuses.Ambiguous;

    private sealed record DeliveryMessage(
        string Kind,
        string Text,
        Embed? Embed,
        MessageComponent? Components);
}
