using DonBot.Core.Models.Entities;
using DonBot.Core.Models.Enums;
using DonBot.Services.DiscordServices;
using Microsoft.EntityFrameworkCore;

namespace DonBot.Api.Services;

public interface IUploadPlayerPointRankingService
{
    Task PublishAsync(long guildId, long fightLogId, CancellationToken ct = default);
}

public sealed class UploadPlayerPointRankingService(
    IDbContextFactory<DatabaseContext> dbContextFactory,
    DiscordRestClientProvider clientProvider,
    PlayerPointRankingPublisher publisher) : IUploadPlayerPointRankingService
{
    public async Task PublishAsync(long guildId, long fightLogId, CancellationToken ct = default)
    {
        await using var context = await dbContextFactory.CreateDbContextAsync(ct);
        var guild = await context.Guild.FirstOrDefaultAsync(item => item.GuildId == guildId, ct);
        if (guild is not { PlayerPointRankingsEnabled: true, PlayerPointRankingsChannelId: > 0 })
        {
            return;
        }

        if (!await context.FightLog.AnyAsync(fight => fight.FightLogId == fightLogId &&
                fight.GuildId == guildId && fight.FightType == (short)FightTypesEnum.WvW, ct))
        {
            return;
        }

        var client = await clientProvider.GetClientAsync();
        await publisher.PublishAsync(client, guild, fightLogId, ct);
    }
}
