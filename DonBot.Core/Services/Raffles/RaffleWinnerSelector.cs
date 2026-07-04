using DonBot.Core.Models.Entities;

namespace DonBot.Core.Services.Raffles;

public interface IRaffleRandomSource
{
    double NextDouble();
}

public sealed class SharedRaffleRandomSource : IRaffleRandomSource
{
    public double NextDouble() => Random.Shared.NextDouble();
}

public sealed class RaffleWinnerSelector(IRaffleRandomSource randomSource)
{
    public IReadOnlyList<PlayerRaffleBid> PickWinners(IReadOnlyList<PlayerRaffleBid> bids, int requestedCount)
    {
        var remaining = bids
            .Where(b => b.PointsSpent > 0)
            .Select(b => new PlayerRaffleBid
            {
                RaffleId = b.RaffleId,
                DiscordId = b.DiscordId,
                PointsSpent = b.PointsSpent
            })
            .ToList();
        var winners = new List<PlayerRaffleBid>();

        while (remaining.Count > 0 && winners.Count < requestedCount)
        {
            var total = remaining.Sum(b => b.PointsSpent);
            if (total <= 0)
            {
                break;
            }

            var picked = (decimal)randomSource.NextDouble() * total;
            var rolling = 0m;
            for (var i = 0; i < remaining.Count; i++)
            {
                rolling += remaining[i].PointsSpent;
                if (picked <= rolling)
                {
                    winners.Add(remaining[i]);
                    remaining.RemoveAt(i);
                    break;
                }
            }
        }

        return winners;
    }
}
