using Discord.Rest;
using DonBot.Services.DatabaseServices;
using DonBot.Services.GuildWarsServices;

namespace DonBot.Api.Services;

public interface IRaidAlertNotifier
{
    Task PostRaidStartedAsync(long guildId, CancellationToken ct = default);
}

// Mirrors RaidCommandCommandService.StartRaid's @everyone alert. Failures are logged but
// never throw, so a misconfigured channel or Discord outage can't undo a successful raid open.
public sealed class RaidAlertNotifier(
    IEntityService entityService,
    IMessageGenerationService messageGeneration,
    DiscordRestClientProvider clientProvider,
    ILogger<RaidAlertNotifier> logger) : IRaidAlertNotifier
{
    public async Task PostRaidStartedAsync(long guildId, CancellationToken ct = default)
    {
        try
        {
            var guild = await entityService.Guild.GetFirstOrDefaultAsync(g => g.GuildId == guildId);
            if (guild == null || !guild.RaidAlertEnabled || guild.RaidAlertChannelId == null)
            {
                return;
            }

            var client = await clientProvider.GetClientAsync();
            var channel = await client.GetChannelAsync((ulong)guild.RaidAlertChannelId);
            if (channel is not RestTextChannel textChannel)
            {
                logger.LogWarning("Raid alert channel {ChannelId} for guild {GuildId} is not a text channel.",
                    guild.RaidAlertChannelId, guildId);
                return;
            }

            var embed = await messageGeneration.GenerateRaidAlert(guildId);
            await textChannel.SendMessageAsync(text: "@everyone", embeds: [embed]);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to post raid alert for guild {GuildId}.", guildId);
        }
    }
}
