using Discord;
using Discord.WebSocket;
using DonBot.Core.Models.Entities;
using DonBot.Services.GuildWarsServices.MessageGeneration;
using Microsoft.Extensions.Logging;

namespace DonBot.Services.DiscordServices;

public sealed class PlayerPointRankingPublisher(
    IPlayerPointRankingService rankingService,
    ILogger<PlayerPointRankingPublisher> logger)
{
    public async Task PublishAsync(IDiscordClient client, Guild guild, long fightLogId, CancellationToken ct = default)
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

        try
        {
            var options = new RequestOptions { CancelToken = ct };
            if (await client.GetChannelAsync((ulong)guild.PlayerPointRankingsChannelId.Value, options: options) is not ITextChannel channel ||
                channel.GuildId != (ulong)guild.GuildId)
            {
                logger.LogWarning(
                    "Failed to find player point rankings channel {ChannelId} for guild {GuildId}.",
                    guild.PlayerPointRankingsChannelId,
                    guild.GuildId);
                return;
            }

            var discordGuild = await client.GetGuildAsync((ulong)guild.GuildId, options: options);
            if (discordGuild == null)
            {
                logger.LogWarning("Failed to find Discord guild {GuildId} for player point rankings.", guild.GuildId);
                return;
            }

            if (discordGuild is SocketGuild socketGuild)
            {
                await socketGuild.DownloadUsersAsync();
            }
            var guildMemberDiscordIds = (await discordGuild.GetUsersAsync(options: options))
                .Select(user => (long)user.Id)
                .ToHashSet();
            var embeds = await rankingService.Generate(guild, fightLogId, guildMemberDiscordIds);
            var recentMessages = await channel.GetMessagesAsync(100, options: options).FlattenAsync();
            var oldRankings = recentMessages.Where(message => IsPointRankingMessage(message, client.CurrentUser.Id)).ToList();
            foreach (var message in oldRankings)
            {
                try
                {
                    await message.DeleteAsync(options);
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Failed to delete old player point ranking message {MessageId}.", message.Id);
                }
            }

            foreach (var embed in embeds)
            {
                await channel.SendMessageAsync(embeds: [embed], options: options);
            }

            logger.LogInformation(
                "Posted player point rankings to channel {ChannelId} for fight {FightLogId} in guild {GuildId}.",
                guild.PlayerPointRankingsChannelId,
                fightLogId,
                guild.GuildId);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
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

    private static bool IsPointRankingMessage(IMessage message, ulong botId) =>
        message.Author.Id == botId &&
        message.Embeds.Any(embed => string.Equals(
            embed.Title,
            PlayerPointRankingService.EmbedTitle,
            StringComparison.Ordinal));
}
