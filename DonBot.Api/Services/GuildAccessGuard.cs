using System.Security.Claims;
using DonBot.Core.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace DonBot.Api.Services;

public sealed class GuildAccessGuard(
    IUserGuildsService userGuilds,
    IDbContextFactory<DatabaseContext> dbContextFactory,
    DiscordRestClientProvider clientProvider)
{
    public async Task<IResult?> RequireMemberAsync(
        ClaimsPrincipal user,
        long guildId,
        CancellationToken ct = default,
        object? forbiddenPayload = null)
    {
        if (!GuildRouteParser.TryNormalize(guildId, out var route))
        {
            return Results.BadRequest("Invalid guild id.");
        }

        return await userGuilds.IsMemberAsync(user, route.UnsignedValue, ct)
            ? null
            : Forbidden(forbiddenPayload);
    }

    public async Task<IResult?> RequireAdministratorAsync(
        ClaimsPrincipal user,
        GuildRouteId guildId,
        CancellationToken ct = default)
    {
        return await userGuilds.HasAdministratorAsync(user, guildId.UnsignedValue, ct)
            ? null
            : Results.Forbid();
    }

    public async Task<IResult?> RequireSchedulingAccessAsync(
        ClaimsPrincipal user,
        GuildRouteId guildId,
        CancellationToken ct = default)
    {
        return await HasSchedulingAccessAsync(user, guildId.UnsignedValue, ct)
            ? null
            : Results.Forbid();
    }

    public async Task<bool> HasSchedulingAccessAsync(
        ClaimsPrincipal user,
        ulong guildId,
        CancellationToken ct = default)
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

        await using var ctx = await dbContextFactory.CreateDbContextAsync(ct);
        var guildIdLong = (long)guildId;
        var managerCsv = await ctx.Guild
            .Where(g => g.GuildId == guildIdLong)
            .Select(g => g.ScheduledEventManagerRoleIds)
            .FirstOrDefaultAsync(ct);
        var managerRoles = ParseRoleIds(managerCsv);
        if (managerRoles.Count == 0)
        {
            return false;
        }

        try
        {
            var client = await clientProvider.GetClientAsync();
            var botGuild = await client.GetGuildAsync(guildId);
            if (botGuild is null)
            {
                return false;
            }

            var member = await botGuild.GetUserAsync(userId);
            return member is not null && member.RoleIds.Any(managerRoles.Contains);
        }
        catch
        {
            return false;
        }
    }

    public static HashSet<ulong> ParseRoleIds(string? csv)
    {
        if (string.IsNullOrWhiteSpace(csv))
        {
            return [];
        }

        return csv
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(s => ulong.TryParse(s, out var v) ? v : 0UL)
            .Where(v => v != 0)
            .ToHashSet();
    }

    private static IResult Forbidden(object? payload) =>
        payload is null
            ? Results.Forbid()
            : Results.Json(payload, statusCode: StatusCodes.Status403Forbidden);
}
