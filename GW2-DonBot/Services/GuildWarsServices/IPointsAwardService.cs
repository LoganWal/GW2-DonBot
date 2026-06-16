using DonBot.Core.Models.Entities;

namespace DonBot.Services.GuildWarsServices;

public interface IPointsAwardService
{
    Task<IReadOnlyList<PlayerPointAward>> AwardFightAsync(long fightLogId, CancellationToken ct = default);
}
