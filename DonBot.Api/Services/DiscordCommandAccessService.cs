using System.Security.Claims;
using Discord;
using Discord.Rest;
using Microsoft.Extensions.Caching.Memory;

namespace DonBot.Api.Services;

public interface IDiscordCommandAccessService
{
    Task<bool> HasCommandAccessAsync(ClaimsPrincipal user, ulong guildId, string commandName, CancellationToken ct = default);
}

public sealed class DiscordCommandAccessService(
    IUserGuildsService userGuilds,
    DiscordRestClientProvider clientProvider,
    IMemoryCache cache,
    ILogger<DiscordCommandAccessService> logger) : IDiscordCommandAccessService
{
    private static readonly TimeSpan CommandCacheTtl = TimeSpan.FromSeconds(60);

    public async Task<bool> HasCommandAccessAsync(ClaimsPrincipal user, ulong guildId, string commandName, CancellationToken ct = default)
    {
        if (await userGuilds.HasAdministratorAsync(user, guildId, ct))
        {
            return true;
        }

        if (!await userGuilds.IsMemberAsync(user, guildId, ct))
        {
            return false;
        }

        var discordIdRaw = user.FindFirst("discord_id")?.Value;
        if (!ulong.TryParse(discordIdRaw, out var userId))
        {
            return false;
        }

        try
        {
            var client = await clientProvider.GetClientAsync();
            var guild = await client.GetGuildAsync(guildId);
            if (guild is null)
            {
                return false;
            }

            var member = await guild.GetUserAsync(userId);
            if (member is null)
            {
                return false;
            }

            var command = await GetCommandAsync(guild, commandName);
            if (command is null)
            {
                return false;
            }

            if (!DefaultMemberPermissionsAllow(command, member))
            {
                return false;
            }

            var permissions = await command.GetCommandPermission();
            return PermissionsAllow(permissions?.Permissions, guildId, userId, member.RoleIds);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to evaluate Discord command access for {Command} in guild {GuildId}.", commandName, guildId);
            return false;
        }
    }

    private Task<RestGuildCommand?> GetCommandAsync(RestGuild guild, string commandName)
    {
        var key = $"discord-command:{guild.Id}:{commandName}";
        return cache.GetOrCoalesceAsync(key, CommandCacheTtl, TimeSpan.FromSeconds(5), async () =>
        {
            var commands = await guild.GetApplicationCommandsAsync();
            return commands.OfType<RestGuildCommand>()
                .FirstOrDefault(c => string.Equals(c.Name, commandName, StringComparison.Ordinal));
        });
    }

    private static bool DefaultMemberPermissionsAllow(RestGuildCommand command, RestGuildUser member)
    {
        var required = command.DefaultMemberPermissions;
        if (required.RawValue == 0)
        {
            return true;
        }

        return (member.GuildPermissions.RawValue & required.RawValue) == required.RawValue;
    }

    internal static bool PermissionsAllow(
        IReadOnlyCollection<ApplicationCommandPermission>? permissions,
        ulong guildId,
        ulong userId,
        IReadOnlyCollection<ulong> roleIds)
    {
        if (permissions is null || permissions.Count == 0)
        {
            return true;
        }

        var userRule = permissions.FirstOrDefault(p =>
            p.TargetType == ApplicationCommandPermissionTarget.User && p.TargetId == userId);
        if (userRule is not null)
        {
            return userRule.Permission;
        }

        var roleRules = permissions
            .Where(p => p.TargetType == ApplicationCommandPermissionTarget.Role && roleIds.Contains(p.TargetId))
            .ToList();
        if (roleRules.Any(p => p.Permission))
        {
            return true;
        }
        if (roleRules.Any(p => !p.Permission))
        {
            return false;
        }

        var everyoneRule = permissions.FirstOrDefault(p =>
            p.TargetType == ApplicationCommandPermissionTarget.Role && p.TargetId == guildId);
        if (everyoneRule is not null)
        {
            return everyoneRule.Permission;
        }

        var allChannelsRule = permissions.FirstOrDefault(p =>
            p.TargetType == ApplicationCommandPermissionTarget.Channel && p.TargetId == guildId - 1);
        if (allChannelsRule is not null)
        {
            return allChannelsRule.Permission;
        }

        return true;
    }
}
