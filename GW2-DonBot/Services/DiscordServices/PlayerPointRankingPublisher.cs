using Discord;
using Discord.WebSocket;
using DonBot.Core.Models.Entities;
using DonBot.Services.GuildWarsServices.MessageGeneration;
using Microsoft.Extensions.Logging;

namespace DonBot.Services.DiscordServices;

public sealed class PlayerPointRankingPublisher(
    DiscordSocketClient client,
    IPlayerPointRankingService rankingService,
    ILogger<PlayerPointRankingPublisher> logger)
{
    public async Task PublishAsync(Guild guild, long fightLogId)
    {
        if (!guild.PlayerPointRankingsEnabled)
        {
            return;
        }

        if (!guild.PlayerPointRankingsChannelId.HasValue)
        {
            logger.LogWarning("Player point rankings are enabled without a channel for guild {GuildId}.", guild.GuildId);
            return;
        }

        if (client.GetChannel((ulong)guild.PlayerPointRankingsChannelId.Value) is not ITextChannel channel)
        {
            logger.LogWarning(
                "Failed to find player point rankings channel {ChannelId} for guild {GuildId}.",
                guild.PlayerPointRankingsChannelId,
                guild.GuildId);
            return;
        }

        try
        {
            var embeds = await rankingService.Generate(guild, fightLogId);
            var recentMessages = await channel.GetMessagesAsync(100).FlattenAsync();
            var oldRankings = recentMessages.Where(IsPointRankingMessage).ToList();
            foreach (var message in oldRankings)
            {
                try
                {
                    await message.DeleteAsync();
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Failed to delete old player point ranking message {MessageId}.", message.Id);
                }
            }

            foreach (var embed in embeds)
            {
                await channel.SendMessageAsync(embeds: [embed]);
            }

            logger.LogInformation(
                "Posted player point rankings to channel {ChannelId} for fight {FightLogId} in guild {GuildId}.",
                guild.PlayerPointRankingsChannelId,
                fightLogId,
                guild.GuildId);
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Failed to post player point rankings for fight {FightLogId} in guild {GuildId}.",
                fightLogId,
                guild.GuildId);
        }
    }

    private bool IsPointRankingMessage(IMessage message) =>
        message.Author.Id == client.CurrentUser.Id &&
        message.Embeds.Any(embed => string.Equals(
            embed.Title,
            PlayerPointRankingService.EmbedTitle,
            StringComparison.Ordinal));
}
