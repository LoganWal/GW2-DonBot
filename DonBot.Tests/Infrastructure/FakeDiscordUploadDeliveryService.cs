using DonBot.Api.Services;
using DonBot.Core.Models.Entities;
using DonBot.Core.Models.GuildWars2;

namespace DonBot.Tests.Infrastructure;

internal sealed class FakeDiscordUploadDeliveryService : IDiscordUploadDeliveryService
{
    public DiscordDeliveryCapabilities Capabilities { get; set; } = new(false, false, false, [], []);

    public DiscordDeliveryValidationResult Validation { get; set; } =
        DiscordDeliveryValidationResult.Failed("discord_delivery_disabled");

    public DiscordDeliveryResult Result { get; set; } = DiscordDeliveryResult.NotRequested;

    public List<long> DeliveredUploadIds { get; } = [];

    public List<long> FailedUploadIds { get; } = [];

    public Task<DiscordDeliveryCapabilities> GetCapabilitiesAsync(
        Guild guild,
        long discordId,
        CancellationToken ct = default) => Task.FromResult(Capabilities);

    public Task<DiscordDeliveryValidationResult> ValidateAsync(
        long discordId,
        long guildId,
        string mode,
        long? channelId,
        CancellationToken ct = default) => Task.FromResult(Validation);

    public Task<DiscordDeliveryResult> DeliverAsync(
        LogUpload upload,
        EliteInsightDataModel data,
        CancellationToken ct = default)
    {
        DeliveredUploadIds.Add(upload.LogUploadId);
        return Task.FromResult(Result);
    }

    public Task<DiscordDeliveryResult> GetResultAsync(long uploadId, CancellationToken ct = default) =>
        Task.FromResult(Result);

    public Task<DiscordDeliveryResult> RecordFailureAsync(
        long uploadId,
        string failureCode,
        CancellationToken ct = default)
    {
        FailedUploadIds.Add(uploadId);
        return Task.FromResult(Result);
    }

    public Task NormalizeInterruptedAsync(CancellationToken ct = default) => Task.CompletedTask;
}
