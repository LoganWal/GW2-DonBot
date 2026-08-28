using DonBot.Api.Services;

namespace DonBot.Tests.Infrastructure;

internal sealed class FakeAggregateDiscordDeliveryService : IAggregateDiscordDeliveryService
{
    public AggregateDiscordDeliveryAttempt Attempt { get; set; } =
        AggregateDiscordDeliveryAttempt.Completed(
            new AggregateDiscordDeliveryResult(
                2,
                DiscordDeliveryResult.FromCounts(1, 0, 0, 0)));

    public List<AggregateDiscordDeliveryRequest> Requests { get; } = [];

    public Task<AggregateDiscordDeliveryAttempt> DeliverAsync(
        AggregateDiscordDeliveryRequest request,
        CancellationToken ct = default)
    {
        Requests.Add(request);
        return Task.FromResult(Attempt);
    }
}
