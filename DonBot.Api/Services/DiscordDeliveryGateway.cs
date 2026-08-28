using Discord;
using Discord.Rest;

namespace DonBot.Api.Services;

public sealed record DiscordAuthorizedChannel(long ChannelId, string ChannelName);

public enum DiscordChannelAuthorizationStatus
{
    Authorized,
    Forbidden,
    Unavailable
}

public sealed record DiscordChannelAuthorizationResult(DiscordChannelAuthorizationStatus Status);

public interface IDiscordDeliveryGateway
{
    Task<IReadOnlyList<DiscordAuthorizedChannel>> GetAuthorizedChannelsAsync(
        long discordId,
        long guildId,
        CancellationToken ct = default);

    Task<bool> IsAuthorizedChannelAsync(
        long discordId,
        long guildId,
        long channelId,
        CancellationToken ct = default);

    async Task<DiscordChannelAuthorizationResult> AuthorizeChannelAsync(
        long discordId,
        long guildId,
        long channelId,
        CancellationToken ct = default) =>
        await IsAuthorizedChannelAsync(discordId, guildId, channelId, ct)
            ? new DiscordChannelAuthorizationResult(DiscordChannelAuthorizationStatus.Authorized)
            : new DiscordChannelAuthorizationResult(DiscordChannelAuthorizationStatus.Forbidden);

    Task<long> SendMessageAsync(
        long channelId,
        string text,
        Embed? embed,
        MessageComponent? components,
        CancellationToken ct = default);
}

public sealed class DiscordDeliveryGateway(
    DiscordRestClientProvider clientProvider,
    ILogger<DiscordDeliveryGateway> logger) : IDiscordDeliveryGateway
{
    public async Task<IReadOnlyList<DiscordAuthorizedChannel>> GetAuthorizedChannelsAsync(
        long discordId,
        long guildId,
        CancellationToken ct = default)
    {
        var access = await ResolveAccessAsync(discordId, guildId, ct);
        if (access.Access is null)
        {
            return [];
        }

        var channels = await access.Access.Guild.GetTextChannelsAsync(new RequestOptions { CancelToken = ct });
        return channels
            .Where(channel => HasRequiredPermissions(access.Access.Member, channel) &&
                              HasRequiredPermissions(access.Access.Bot, channel))
            .OrderBy(channel => channel.Position)
            .ThenBy(channel => channel.Name, StringComparer.OrdinalIgnoreCase)
            .Select(channel => new DiscordAuthorizedChannel((long)channel.Id, NormalizeName(channel.Name)))
            .ToList();
    }

    public async Task<bool> IsAuthorizedChannelAsync(
        long discordId,
        long guildId,
        long channelId,
        CancellationToken ct = default)
    {
        return (await AuthorizeChannelAsync(discordId, guildId, channelId, ct)).Status ==
               DiscordChannelAuthorizationStatus.Authorized;
    }

    public async Task<DiscordChannelAuthorizationResult> AuthorizeChannelAsync(
        long discordId,
        long guildId,
        long channelId,
        CancellationToken ct = default)
    {
        if (channelId <= 0)
        {
            return new DiscordChannelAuthorizationResult(DiscordChannelAuthorizationStatus.Forbidden);
        }

        var access = await ResolveAccessAsync(discordId, guildId, ct);
        if (access.Status == DiscordChannelAuthorizationStatus.Unavailable)
        {
            return new DiscordChannelAuthorizationResult(DiscordChannelAuthorizationStatus.Unavailable);
        }

        if (access.Access is null)
        {
            return new DiscordChannelAuthorizationResult(DiscordChannelAuthorizationStatus.Forbidden);
        }

        try
        {
            var channel = await access.Access.Guild.GetTextChannelAsync(
                (ulong)channelId,
                new RequestOptions { CancelToken = ct });
            return channel is not null &&
                   HasRequiredPermissions(access.Access.Member, channel) &&
                   HasRequiredPermissions(access.Access.Bot, channel)
                ? new DiscordChannelAuthorizationResult(DiscordChannelAuthorizationStatus.Authorized)
                : new DiscordChannelAuthorizationResult(DiscordChannelAuthorizationStatus.Forbidden);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                "Discord channel authorization failed with exception type {ExceptionType}.",
                ex.GetType().Name);
            return new DiscordChannelAuthorizationResult(DiscordChannelAuthorizationStatus.Unavailable);
        }
    }

    public async Task<long> SendMessageAsync(
        long channelId,
        string text,
        Embed? embed,
        MessageComponent? components,
        CancellationToken ct = default)
    {
        var client = await clientProvider.GetClientAsync();
        var channel = await client.GetChannelAsync(
            (ulong)channelId,
            new RequestOptions { CancelToken = ct });
        if (channel is not ITextChannel textChannel)
        {
            throw new InvalidOperationException("Discord destination is unavailable.");
        }

        var message = await textChannel.SendMessageAsync(
            text: text,
            embed: embed,
            components: components,
            options: new RequestOptions { CancelToken = ct });
        return (long)message.Id;
    }

    private async Task<DiscordAccessResolution> ResolveAccessAsync(long discordId, long guildId, CancellationToken ct)
    {
        if (discordId <= 0 || guildId <= 0)
        {
            return new DiscordAccessResolution(null, DiscordChannelAuthorizationStatus.Forbidden);
        }

        try
        {
            var client = await clientProvider.GetClientAsync();
            var options = new RequestOptions { CancelToken = ct };
            var guild = await client.GetGuildAsync((ulong)guildId, options);
            if (guild is null)
            {
                return new DiscordAccessResolution(null, DiscordChannelAuthorizationStatus.Forbidden);
            }

            var member = await guild.GetUserAsync((ulong)discordId, options);
            var bot = await guild.GetUserAsync(client.CurrentUser.Id, options);
            return member is null || bot is null
                ? new DiscordAccessResolution(null, DiscordChannelAuthorizationStatus.Forbidden)
                : new DiscordAccessResolution(
                    new DiscordAccess(guild, member, bot),
                    DiscordChannelAuthorizationStatus.Authorized);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                "Discord delivery access resolution failed with exception type {ExceptionType}.",
                ex.GetType().Name);
            return new DiscordAccessResolution(null, DiscordChannelAuthorizationStatus.Unavailable);
        }
    }

    private static bool HasRequiredPermissions(RestGuildUser user, RestTextChannel channel)
    {
        return HasRequiredPermissions(user.GetPermissions(channel));
    }

    internal static bool HasRequiredPermissions(ChannelPermissions permissions)
    {
        return permissions.ViewChannel && permissions.SendMessages && permissions.EmbedLinks;
    }

    private static string NormalizeName(string value)
    {
        var sanitized = new string(value.Where(character => character >= ' ' && character != '\u007f').ToArray());
        while (System.Text.Encoding.UTF8.GetByteCount(sanitized) > 256)
        {
            sanitized = sanitized[..^1];
        }

        return sanitized;
    }

    private sealed record DiscordAccess(RestGuild Guild, RestGuildUser Member, RestGuildUser Bot);

    private sealed record DiscordAccessResolution(
        DiscordAccess? Access,
        DiscordChannelAuthorizationStatus Status);
}
