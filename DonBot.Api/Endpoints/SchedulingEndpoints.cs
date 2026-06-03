using System.Security.Claims;
using Discord;
using DonBot.Api.Services;
using DonBot.Models.Entities;
using DonBot.Models.Enums;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace DonBot.Api.Endpoints;

public static class SchedulingEndpoints
{
    public static void MapSchedulingEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/scheduling").RequireAuthorization();
        group.MapGet("/guilds", ListGuilds);
        group.MapGet("/guilds/{guildId}/context", GetGuildContext);
        group.MapGet("/guilds/{guildId}/events", ListEvents);
        group.MapPost("/guilds/{guildId}/events", CreateEvent);
        group.MapPut("/guilds/{guildId}/events/{eventId:long}", UpdateEvent);
        group.MapDelete("/guilds/{guildId}/events/{eventId:long}", DeleteEvent);
    }

    public record GuildSummaryDto(string GuildId, string Name, string? IconUrl);

    public record ChannelDto(string Id, string Name);

    public record RoleDto(string Id, string Name);

    public record EventDto(
        long ScheduledEventId,
        short EventType,
        string ChannelId,
        short Day,
        short Hour,
        short RepeatIntervalDays,
        string Message,
        DateTime UtcEventTime);

    public record EventWriteDto(
        short EventType,
        string ChannelId,
        short Day,
        short Hour,
        DateTime UtcEventTime,
        short RepeatIntervalDays,
        string? Message);

    public record GuildContextDto(
        string GuildId,
        string GuildName,
        IReadOnlyList<ChannelDto> Channels,
        IReadOnlyList<RoleDto> Roles);

    internal const int MaxMessageLength = 256;

    private static readonly TimeSpan AccessCacheTtl = TimeSpan.FromSeconds(60);

    private static async Task<IResult> ListGuilds(
        ClaimsPrincipal user,
        [FromServices] IUserGuildsService userGuilds,
        [FromServices] IDbContextFactory<DatabaseContext> dbContextFactory,
        [FromServices] DiscordRestClientProvider clientProvider,
        [FromServices] IMemoryCache cache)
    {
        var discordId = user.FindFirst("discord_id")?.Value;
        if (string.IsNullOrEmpty(discordId)) {
            return Results.Unauthorized();
        }

        var cacheKey = $"scheduling-guilds:{discordId}";
        var result = await cache.GetOrCoalesceAsync(cacheKey, AccessCacheTtl, TimeSpan.FromSeconds(5), async () =>
        {
            var list = await userGuilds.GetForPrincipalAsync(user);
            if (list is null) {
                return new List<GuildSummaryDto>();
            }

            await using var ctx = await dbContextFactory.CreateDbContextAsync();
            var tracked = await ctx.Guild
                .Select(g => new { g.GuildId, g.GuildName, g.ScheduledEventManagerRoleIds })
                .ToListAsync();
            var trackedById = tracked.ToDictionary(g => (ulong)g.GuildId);

            if (!ulong.TryParse(discordId, out var userIdUlong)) {
                return new List<GuildSummaryDto>();
            }

            var client = await clientProvider.GetClientAsync();
            var accessible = new List<GuildSummaryDto>();
            foreach (var ug in list)
            {
                if (!trackedById.TryGetValue(ug.Id, out var entry)) {
                    continue;
                }

                if (UserGuildsService.HasAdministrator(ug))
                {
                    accessible.Add(new GuildSummaryDto(ug.Id.ToString(), ug.Name, UserGuildsService.BuildIconUrl(ug.Id, ug.Icon)));
                    continue;
                }

                var managerRoleIds = ParseRoleIds(entry.ScheduledEventManagerRoleIds);
                if (managerRoleIds.Count == 0) {
                    continue;
                }

                try
                {
                    var botGuild = await client.GetGuildAsync(ug.Id);
                    if (botGuild is null) {
                        continue;
                    }
                    var member = await botGuild.GetUserAsync(userIdUlong);
                    if (member is null) {
                        continue;
                    }
                    if (member.RoleIds.Any(r => managerRoleIds.Contains(r)))
                    {
                        accessible.Add(new GuildSummaryDto(ug.Id.ToString(), ug.Name, UserGuildsService.BuildIconUrl(ug.Id, ug.Icon)));
                    }
                }
                catch
                {
                    // ignored: lack of access from bot side just means this guild won't appear
                }
            }

            return accessible.OrderBy(g => g.Name).ToList();
        });

        return Results.Ok(result);
    }

    private static async Task<IResult> GetGuildContext(
        string guildId,
        ClaimsPrincipal user,
        [FromServices] IUserGuildsService userGuilds,
        [FromServices] IDbContextFactory<DatabaseContext> dbContextFactory,
        [FromServices] DiscordRestClientProvider clientProvider)
    {
        if (!ulong.TryParse(guildId, out var guildIdUlong)) {
            return Results.BadRequest("Invalid guild id.");
        }

        if (!await HasSchedulingAccessAsync(user, guildIdUlong, userGuilds, dbContextFactory, clientProvider)) {
            return Results.Forbid();
        }

        var client = await clientProvider.GetClientAsync();
        var botGuild = await client.GetGuildAsync(guildIdUlong);
        if (botGuild is null) {
            return Results.NotFound("Bot is not in that guild.");
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

        return Results.Ok(new GuildContextDto(guildId, botGuild.Name, channels, roles));
    }

    private static async Task<IResult> ListEvents(
        string guildId,
        ClaimsPrincipal user,
        [FromServices] IUserGuildsService userGuilds,
        [FromServices] IDbContextFactory<DatabaseContext> dbContextFactory,
        [FromServices] DiscordRestClientProvider clientProvider)
    {
        if (!ulong.TryParse(guildId, out var guildIdUlong)) {
            return Results.BadRequest("Invalid guild id.");
        }
        if (!await HasSchedulingAccessAsync(user, guildIdUlong, userGuilds, dbContextFactory, clientProvider)) {
            return Results.Forbid();
        }

        var guildIdLong = (long)guildIdUlong;
        await using var ctx = await dbContextFactory.CreateDbContextAsync();
        var events = await ctx.ScheduledEvent
            .Where(e => e.GuildId == guildIdLong)
            .OrderBy(e => e.UtcEventTime)
            .ToListAsync();

        return Results.Ok(events.Select(ToDto).ToList());
    }

    private static async Task<IResult> CreateEvent(
        string guildId,
        EventWriteDto body,
        ClaimsPrincipal user,
        [FromServices] IUserGuildsService userGuilds,
        [FromServices] IDbContextFactory<DatabaseContext> dbContextFactory,
        [FromServices] DiscordRestClientProvider clientProvider)
    {
        if (!ulong.TryParse(guildId, out var guildIdUlong)) {
            return Results.BadRequest("Invalid guild id.");
        }
        if (!await HasSchedulingAccessAsync(user, guildIdUlong, userGuilds, dbContextFactory, clientProvider)) {
            return Results.Forbid();
        }

        var validChannelIds = await GetValidChannelIdsAsync(guildIdUlong, clientProvider);
        if (validChannelIds is null) {
            return Results.NotFound("Bot is not in that guild.");
        }

        var error = ValidateEvent(body, validChannelIds);
        if (error is not null) {
            return Results.BadRequest(error);
        }

        var guildIdLong = (long)guildIdUlong;
        var entity = BuildEvent(guildIdLong, body);

        await using var ctx = await dbContextFactory.CreateDbContextAsync();
        ctx.ScheduledEvent.Add(entity);
        await ctx.SaveChangesAsync();
        return Results.Ok(ToDto(entity));
    }

    private static ScheduledEvent BuildEvent(long guildIdLong, EventWriteDto body)
    {
        var utc = DateTime.SpecifyKind(body.UtcEventTime, DateTimeKind.Utc);
        return new ScheduledEvent
        {
            GuildId = guildIdLong,
            EventType = body.EventType,
            ChannelId = long.Parse(body.ChannelId),
            Day = body.Day,
            Hour = body.Hour,
            RepeatIntervalDays = body.RepeatIntervalDays,
            Message = (body.Message ?? string.Empty).Trim(),
            UtcEventTime = utc
        };
    }

    private static async Task<IResult> UpdateEvent(
        string guildId,
        long eventId,
        EventWriteDto body,
        ClaimsPrincipal user,
        [FromServices] IUserGuildsService userGuilds,
        [FromServices] IDbContextFactory<DatabaseContext> dbContextFactory,
        [FromServices] DiscordRestClientProvider clientProvider)
    {
        if (!ulong.TryParse(guildId, out var guildIdUlong)) {
            return Results.BadRequest("Invalid guild id.");
        }
        if (!await HasSchedulingAccessAsync(user, guildIdUlong, userGuilds, dbContextFactory, clientProvider)) {
            return Results.Forbid();
        }

        var validChannelIds = await GetValidChannelIdsAsync(guildIdUlong, clientProvider);
        if (validChannelIds is null) {
            return Results.NotFound("Bot is not in that guild.");
        }

        var error = ValidateEvent(body, validChannelIds);
        if (error is not null) {
            return Results.BadRequest(error);
        }

        var guildIdLong = (long)guildIdUlong;
        await using var ctx = await dbContextFactory.CreateDbContextAsync();
        var existing = await ctx.ScheduledEvent.AsTracking()
            .FirstOrDefaultAsync(e => e.ScheduledEventId == eventId && e.GuildId == guildIdLong);
        if (existing is null) {
            return Results.NotFound();
        }

        // Replace the row because scheduler fields are init-only and may be cached.
        var priorChannelId = existing.ChannelId;
        var priorMessageId = existing.MessageId;

        ctx.ScheduledEvent.Remove(existing);
        var replacement = BuildEvent(guildIdLong, body);
        ctx.ScheduledEvent.Add(replacement);
        await ctx.SaveChangesAsync();

        // Best effort sync for signup messages that were already posted.
        if (priorMessageId.HasValue && IsSignupEvent(replacement.EventType))
        {
            await TryUpdatePostedMessageAsync(
                clientProvider,
                guildIdUlong,
                (ulong)priorChannelId,
                (ulong)priorMessageId.Value,
                replacement.UtcEventTime,
                replacement.Message);
        }

        return Results.Ok(ToDto(replacement));
    }

    private static bool IsSignupEvent(short eventType) =>
        eventType == (short)ScheduledEventTypeEnum.RaidSignup
        || eventType == (short)ScheduledEventTypeEnum.WvwRaidSignup;

    private static async Task TryUpdatePostedMessageAsync(
        DiscordRestClientProvider clientProvider,
        ulong guildId,
        ulong channelId,
        ulong messageId,
        DateTime newUtcEventTime,
        string newMessage)
    {
        try
        {
            var client = await clientProvider.GetClientAsync();
            var botGuild = await client.GetGuildAsync(guildId);
            if (botGuild is null) {
                return;
            }
            var channel = await botGuild.GetTextChannelAsync(channelId);
            if (channel is null) {
                return;
            }
            var message = await channel.GetMessageAsync(messageId);
            if (message is not Discord.Rest.RestUserMessage userMessage) {
                return;
            }

            var unix = new DateTimeOffset(newUtcEventTime, TimeSpan.Zero).ToUnixTimeSeconds();
            var newContent = $"<t:{unix}:f>\n{newMessage}";
            await userMessage.ModifyAsync(p => p.Content = newContent);
        }
        catch
        {
            // ignored: message may have been deleted, perms revoked, etc.
        }
    }

    private static async Task<IResult> DeleteEvent(
        string guildId,
        long eventId,
        ClaimsPrincipal user,
        [FromServices] IUserGuildsService userGuilds,
        [FromServices] IDbContextFactory<DatabaseContext> dbContextFactory,
        [FromServices] DiscordRestClientProvider clientProvider)
    {
        if (!ulong.TryParse(guildId, out var guildIdUlong)) {
            return Results.BadRequest("Invalid guild id.");
        }
        if (!await HasSchedulingAccessAsync(user, guildIdUlong, userGuilds, dbContextFactory, clientProvider)) {
            return Results.Forbid();
        }

        var guildIdLong = (long)guildIdUlong;
        await using var ctx = await dbContextFactory.CreateDbContextAsync();
        var existing = await ctx.ScheduledEvent.AsTracking()
            .FirstOrDefaultAsync(e => e.ScheduledEventId == eventId && e.GuildId == guildIdLong);
        if (existing is null) {
            return Results.NotFound();
        }

        ctx.ScheduledEvent.Remove(existing);
        await ctx.SaveChangesAsync();
        return Results.NoContent();
    }

    private static async Task<HashSet<ulong>?> GetValidChannelIdsAsync(
        ulong guildId, DiscordRestClientProvider clientProvider)
    {
        var client = await clientProvider.GetClientAsync();
        var botGuild = await client.GetGuildAsync(guildId);
        if (botGuild is null) {
            return null;
        }
        return (await botGuild.GetTextChannelsAsync()).Select(c => c.Id).ToHashSet();
    }

    internal static string? ValidateEvent(EventWriteDto body, HashSet<ulong> validChannelIds)
    {
        if (!Enum.IsDefined(typeof(ScheduledEventTypeEnum), body.EventType)) {
            return "Invalid event type.";
        }
        if (body.EventType == (short)3) {
            return "Invalid event type.";
        }
        if (body.Day < 0 || body.Day > 6) {
            return "Post day must be 0-6 (Sunday-Saturday).";
        }
        if (body.Hour < 0 || body.Hour > 23) {
            return "Post hour must be 0-23.";
        }
        if (body.UtcEventTime == default) {
            return "Event time is required.";
        }
        if (body.UtcEventTime <= DateTime.UtcNow) {
            return "Event time must be in the future.";
        }
        if (body.RepeatIntervalDays < 1 || body.RepeatIntervalDays > 365) {
            return "Repeat interval must be 1-365 days.";
        }
        if (!ulong.TryParse(body.ChannelId, out var channelParsed) || !validChannelIds.Contains(channelParsed)) {
            return "Channel does not belong to this guild.";
        }
        if (!string.IsNullOrEmpty(body.Message) && body.Message.Length > MaxMessageLength) {
            return $"Message must be {MaxMessageLength} characters or fewer.";
        }
        return null;
    }

    internal static EventDto ToDto(ScheduledEvent e) => new(
        e.ScheduledEventId,
        e.EventType,
        e.ChannelId.ToString(),
        e.Day,
        e.Hour,
        e.RepeatIntervalDays,
        e.Message,
        e.UtcEventTime);

    internal static HashSet<ulong> ParseRoleIds(string? csv)
    {
        if (string.IsNullOrWhiteSpace(csv)) {
            return [];
        }
        return csv
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(s => ulong.TryParse(s, out var v) ? v : 0UL)
            .Where(v => v != 0)
            .ToHashSet();
    }

    internal static async Task<bool> HasSchedulingAccessAsync(
        ClaimsPrincipal user,
        ulong guildId,
        IUserGuildsService userGuilds,
        IDbContextFactory<DatabaseContext> dbContextFactory,
        DiscordRestClientProvider clientProvider)
    {
        if (await userGuilds.HasAdministratorAsync(user, guildId)) {
            return true;
        }
        if (!await userGuilds.IsMemberAsync(user, guildId)) {
            return false;
        }

        var discordIdRaw = user.FindFirst("discord_id")?.Value;
        if (!ulong.TryParse(discordIdRaw, out var userIdUlong)) {
            return false;
        }

        var guildIdLong = (long)guildId;
        await using var ctx = await dbContextFactory.CreateDbContextAsync();
        var managerCsv = await ctx.Guild
            .Where(g => g.GuildId == guildIdLong)
            .Select(g => g.ScheduledEventManagerRoleIds)
            .FirstOrDefaultAsync();
        var managerRoles = ParseRoleIds(managerCsv);
        if (managerRoles.Count == 0) {
            return false;
        }

        try
        {
            var client = await clientProvider.GetClientAsync();
            var botGuild = await client.GetGuildAsync(guildId);
            if (botGuild is null) {
                return false;
            }
            var member = await botGuild.GetUserAsync(userIdUlong);
            if (member is null) {
                return false;
            }
            return member.RoleIds.Any(r => managerRoles.Contains(r));
        }
        catch
        {
            return false;
        }
    }
}
