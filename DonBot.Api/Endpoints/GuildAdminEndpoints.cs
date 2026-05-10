using System.Security.Claims;
using Discord;
using Discord.Rest;
using DonBot.Api.Services;
using DonBot.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace DonBot.Api.Endpoints;

public static class GuildAdminEndpoints
{
    public static void MapGuildAdminEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/admin").RequireAuthorization();
        group.MapGet("/guilds", ListAdminGuilds);
        group.MapGet("/guilds/{guildId}/config", GetGuildConfig);
        group.MapPut("/guilds/{guildId}/config", UpdateGuildConfig);
    }

    public record GuildSummaryDto(string GuildId, string Name, string? IconUrl);

    public record ChannelDto(string Id, string Name);

    public record RoleDto(string Id, string Name);

    public record GuildConfigDto(
        string? LogDropOffChannelId,
        string? DiscordGuildMemberRoleId,
        string? DiscordSecondaryMemberRoleId,
        string? DiscordVerifiedRoleId,
        string? Gw2GuildMemberRoleId,
        string? Gw2SecondaryMemberRoleIds,
        string? AnnouncementChannelId,
        string? LogReportChannelId,
        string? AdvanceLogReportChannelId,
        string? StreamLogChannelId,
        bool RaidAlertEnabled,
        string? RaidAlertChannelId,
        bool RemoveSpamEnabled,
        string? RemovedMessageChannelId,
        bool AutoSubmitToWingman,
        bool AutoAggregateLogs,
        bool AutoReplySingleLog,
        bool WvwLeaderboardEnabled,
        string? WvwLeaderboardChannelId,
        bool PveLeaderboardEnabled,
        string? PveLeaderboardChannelId);

    public record GuildConfigResponse(
        string GuildId,
        string GuildName,
        GuildConfigDto Config,
        IReadOnlyList<ChannelDto> Channels,
        IReadOnlyList<RoleDto> Roles);

    private static readonly TimeSpan AdminGuildsCacheTtl = TimeSpan.FromSeconds(60);

    private static async Task<IResult> ListAdminGuilds(
        ClaimsPrincipal user,
        DiscordRestClientProvider clientProvider,
        IDbContextFactory<DatabaseContext> dbContextFactory,
        IMemoryCache cache,
        ILogger<DiscordRestClientProvider> logger)
    {
        if (!TryGetDiscordId(user, out var discordId))
            return Results.Unauthorized();

        var cacheKey = $"admin-guilds:{discordId}";
        var cached = await cache.GetOrCreateAsync(cacheKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = AdminGuildsCacheTtl;

            var client = await clientProvider.GetClientAsync();

            await using var context = await dbContextFactory.CreateDbContextAsync();
            var trackedGuildIds = await context.Guild.Select(g => g.GuildId).ToListAsync();
            var trackedSet = trackedGuildIds.Select(id => (ulong)id).ToHashSet();

            var botGuilds = await client.GetGuildsAsync();
            var candidateGuilds = botGuilds.Where(g => trackedSet.Contains(g.Id)).ToList();

            var checks = candidateGuilds.Select(async botGuild =>
            {
                var guildUser = await SafeGetGuildUserAsync(botGuild, discordId, logger);
                if (guildUser is null || !guildUser.GuildPermissions.Administrator)
                    return null;
                return new GuildSummaryDto(botGuild.Id.ToString(), botGuild.Name, botGuild.IconUrl);
            });

            var results = await Task.WhenAll(checks);
            return results.Where(r => r is not null).OrderBy(r => r!.Name).ToList();
        });

        return Results.Ok(cached);
    }

    private static async Task<IResult> GetGuildConfig(
        string guildId,
        ClaimsPrincipal user,
        DiscordRestClientProvider clientProvider,
        IDbContextFactory<DatabaseContext> dbContextFactory,
        ILogger<DiscordRestClientProvider> logger)
    {
        if (!TryGetDiscordId(user, out var discordId))
            return Results.Unauthorized();

        if (!ulong.TryParse(guildId, out var guildIdUlong))
            return Results.BadRequest("Invalid guild id.");

        var client = await clientProvider.GetClientAsync();
        var botGuild = await client.GetGuildAsync(guildIdUlong);
        if (botGuild is null)
            return Results.NotFound("Bot is not in that guild.");

        var guildUser = await SafeGetGuildUserAsync(botGuild, discordId, logger);
        if (guildUser is null || !guildUser.GuildPermissions.Administrator)
            return Results.Forbid();

        await using var context = await dbContextFactory.CreateDbContextAsync();
        var guildIdLong = (long)guildIdUlong;
        var guild = await context.Guild.FirstOrDefaultAsync(g => g.GuildId == guildIdLong);
        if (guild is null)
        {
            guild = new Guild { GuildId = guildIdLong, GuildName = botGuild.Name };
            context.Guild.Add(guild);
            await context.SaveChangesAsync();
        }

        var channels = (await botGuild.GetTextChannelsAsync())
            .OrderBy(c => c.Position)
            .Select(c => new ChannelDto(c.Id.ToString(), c.Name))
            .ToList();

        var roles = botGuild.Roles
            .Where(r => r.Id != botGuild.EveryoneRole.Id && !r.IsManaged)
            .OrderByDescending(r => r.Position)
            .Select(r => new RoleDto(r.Id.ToString(), r.Name))
            .ToList();

        var response = new GuildConfigResponse(
            guildId,
            botGuild.Name,
            ToDto(guild),
            channels,
            roles);

        return Results.Ok(response);
    }

    private static async Task<IResult> UpdateGuildConfig(
        string guildId,
        GuildConfigDto dto,
        ClaimsPrincipal user,
        DiscordRestClientProvider clientProvider,
        IDbContextFactory<DatabaseContext> dbContextFactory,
        ILogger<DiscordRestClientProvider> logger)
    {
        if (!TryGetDiscordId(user, out var discordId))
            return Results.Unauthorized();

        if (!ulong.TryParse(guildId, out var guildIdUlong))
            return Results.BadRequest("Invalid guild id.");

        var client = await clientProvider.GetClientAsync();
        var botGuild = await client.GetGuildAsync(guildIdUlong);
        if (botGuild is null)
            return Results.NotFound("Bot is not in that guild.");

        var guildUser = await SafeGetGuildUserAsync(botGuild, discordId, logger);
        if (guildUser is null || !guildUser.GuildPermissions.Administrator)
            return Results.Forbid();

        var validChannelIds = (await botGuild.GetTextChannelsAsync()).Select(c => c.Id).ToHashSet();
        var validRoleIds = botGuild.Roles.Select(r => r.Id).ToHashSet();

        string? channelError = ValidateOptionalSnowflake(dto.LogDropOffChannelId, validChannelIds, nameof(dto.LogDropOffChannelId))
            ?? ValidateOptionalSnowflake(dto.AnnouncementChannelId, validChannelIds, nameof(dto.AnnouncementChannelId))
            ?? ValidateOptionalSnowflake(dto.LogReportChannelId, validChannelIds, nameof(dto.LogReportChannelId))
            ?? ValidateOptionalSnowflake(dto.AdvanceLogReportChannelId, validChannelIds, nameof(dto.AdvanceLogReportChannelId))
            ?? ValidateOptionalSnowflake(dto.StreamLogChannelId, validChannelIds, nameof(dto.StreamLogChannelId))
            ?? ValidateOptionalSnowflake(dto.RaidAlertChannelId, validChannelIds, nameof(dto.RaidAlertChannelId))
            ?? ValidateOptionalSnowflake(dto.RemovedMessageChannelId, validChannelIds, nameof(dto.RemovedMessageChannelId))
            ?? ValidateOptionalSnowflake(dto.WvwLeaderboardChannelId, validChannelIds, nameof(dto.WvwLeaderboardChannelId))
            ?? ValidateOptionalSnowflake(dto.PveLeaderboardChannelId, validChannelIds, nameof(dto.PveLeaderboardChannelId));

        if (channelError is not null)
            return Results.BadRequest(channelError);

        string? roleError = ValidateOptionalSnowflake(dto.DiscordGuildMemberRoleId, validRoleIds, nameof(dto.DiscordGuildMemberRoleId))
            ?? ValidateOptionalSnowflake(dto.DiscordSecondaryMemberRoleId, validRoleIds, nameof(dto.DiscordSecondaryMemberRoleId))
            ?? ValidateOptionalSnowflake(dto.DiscordVerifiedRoleId, validRoleIds, nameof(dto.DiscordVerifiedRoleId));

        if (roleError is not null)
            return Results.BadRequest(roleError);

        await using var context = await dbContextFactory.CreateDbContextAsync();
        var guildIdLong = (long)guildIdUlong;
        var guild = await context.Guild.AsTracking().FirstOrDefaultAsync(g => g.GuildId == guildIdLong);
        if (guild is null)
        {
            guild = new Guild { GuildId = guildIdLong, GuildName = botGuild.Name };
            context.Guild.Add(guild);
        }

        ApplyDto(guild, dto);

        await context.SaveChangesAsync();

        return Results.Ok(ToDto(guild));
    }

    internal static GuildConfigDto ToDto(Guild g) => new(
        LongToString(g.LogDropOffChannelId),
        LongToString(g.DiscordGuildMemberRoleId),
        LongToString(g.DiscordSecondaryMemberRoleId),
        LongToString(g.DiscordVerifiedRoleId),
        g.Gw2GuildMemberRoleId,
        g.Gw2SecondaryMemberRoleIds,
        LongToString(g.AnnouncementChannelId),
        LongToString(g.LogReportChannelId),
        LongToString(g.AdvanceLogReportChannelId),
        LongToString(g.StreamLogChannelId),
        g.RaidAlertEnabled,
        LongToString(g.RaidAlertChannelId),
        g.RemoveSpamEnabled,
        LongToString(g.RemovedMessageChannelId),
        g.AutoSubmitToWingman,
        g.AutoAggregateLogs,
        g.AutoReplySingleLog,
        g.WvwLeaderboardEnabled,
        LongToString(g.WvwLeaderboardChannelId),
        g.PveLeaderboardEnabled,
        LongToString(g.PveLeaderboardChannelId));

    internal static string? LongToString(long? value) => value?.ToString();

    internal static long? ParseOptionalLong(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : long.Parse(value);

    internal static string? NullIfEmpty(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    internal static void ApplyDto(Guild guild, GuildConfigDto dto)
    {
        guild.LogDropOffChannelId = ParseOptionalLong(dto.LogDropOffChannelId);
        guild.DiscordGuildMemberRoleId = ParseOptionalLong(dto.DiscordGuildMemberRoleId);
        guild.DiscordSecondaryMemberRoleId = ParseOptionalLong(dto.DiscordSecondaryMemberRoleId);
        guild.DiscordVerifiedRoleId = ParseOptionalLong(dto.DiscordVerifiedRoleId);
        guild.Gw2GuildMemberRoleId = NullIfEmpty(dto.Gw2GuildMemberRoleId);
        guild.Gw2SecondaryMemberRoleIds = NullIfEmpty(dto.Gw2SecondaryMemberRoleIds);
        guild.AnnouncementChannelId = ParseOptionalLong(dto.AnnouncementChannelId);
        guild.LogReportChannelId = ParseOptionalLong(dto.LogReportChannelId);
        guild.AdvanceLogReportChannelId = ParseOptionalLong(dto.AdvanceLogReportChannelId);
        guild.StreamLogChannelId = ParseOptionalLong(dto.StreamLogChannelId);
        guild.RaidAlertEnabled = dto.RaidAlertEnabled;
        guild.RaidAlertChannelId = ParseOptionalLong(dto.RaidAlertChannelId);
        guild.RemoveSpamEnabled = dto.RemoveSpamEnabled;
        guild.RemovedMessageChannelId = ParseOptionalLong(dto.RemovedMessageChannelId);
        guild.AutoSubmitToWingman = dto.AutoSubmitToWingman;
        guild.AutoAggregateLogs = dto.AutoAggregateLogs;
        guild.AutoReplySingleLog = dto.AutoReplySingleLog;
        guild.WvwLeaderboardEnabled = dto.WvwLeaderboardEnabled;
        guild.WvwLeaderboardChannelId = ParseOptionalLong(dto.WvwLeaderboardChannelId);
        guild.PveLeaderboardEnabled = dto.PveLeaderboardEnabled;
        guild.PveLeaderboardChannelId = ParseOptionalLong(dto.PveLeaderboardChannelId);
    }

    internal static string? ValidateOptionalSnowflake(string? value, HashSet<ulong> validIds, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;
        if (!ulong.TryParse(value, out var parsed))
            return $"{fieldName} is not a valid id.";
        if (!validIds.Contains(parsed))
            return $"{fieldName} does not belong to this guild.";
        return null;
    }

    private static bool TryGetDiscordId(ClaimsPrincipal user, out ulong discordId)
    {
        discordId = 0;
        var raw = user.FindFirst("discord_id")?.Value;
        return ulong.TryParse(raw, out discordId);
    }

    private static async Task<RestGuildUser?> SafeGetGuildUserAsync(RestGuild guild, ulong userId, ILogger logger)
    {
        try
        {
            return await guild.GetUserAsync(userId);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to fetch user {UserId} in guild {GuildId} ({GuildName}); treating as not-a-member.",
                userId, guild.Id, guild.Name);
            return null;
        }
    }
}
