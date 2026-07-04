namespace DonBot.Api.Services;

public interface IDiscordGuildMembershipService
{
    Task<IReadOnlySet<long>> GetMemberGuildIdsAsync(
        long discordId,
        IReadOnlyCollection<long> guildIds,
        CancellationToken ct = default);
}

public sealed class DiscordGuildMembershipService(
    DiscordRestClientProvider clientProvider,
    ILogger<DiscordGuildMembershipService> logger) : IDiscordGuildMembershipService
{
    public async Task<IReadOnlySet<long>> GetMemberGuildIdsAsync(
        long discordId,
        IReadOnlyCollection<long> guildIds,
        CancellationToken ct = default)
    {
        if (discordId <= 0 || guildIds.Count == 0)
        {
            return new HashSet<long>();
        }

        var userId = (ulong)discordId;
        var client = await clientProvider.GetClientAsync();
        var memberGuildIds = new HashSet<long>();

        foreach (var guildId in guildIds.Distinct())
        {
            ct.ThrowIfCancellationRequested();
            if (guildId <= 0)
            {
                continue;
            }

            try
            {
                var guild = await client.GetGuildAsync((ulong)guildId);
                if (guild is null)
                {
                    continue;
                }

                var member = await guild.GetUserAsync(userId);
                if (member is not null)
                {
                    memberGuildIds.Add(guildId);
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(
                    ex,
                    "Failed to resolve Discord membership for user {DiscordId} in guild {GuildId}.",
                    discordId,
                    guildId);
            }
        }

        return memberGuildIds;
    }
}
