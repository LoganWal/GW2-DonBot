using System.Security.Claims;
using Discord;
using DonBot.Api.Services;
using DonBot.Core.Models.Entities;
using DonBot.Core.Models.Enums;
using DonBot.Core.Models.Scheduling;
using DonBot.Services.SchedulerServices;
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

    public record EventDto(
        long ScheduledEventId,
        short EventType,
        string ChannelId,
        short Day,
        short Hour,
        short RepeatIntervalDays,
        string Message,
        IReadOnlyList<ScheduledEventResponseOption> ResponseOptions,
        DateTime UtcEventTime,
        short NotificationMinutesBeforeStart);

    public record EventWriteDto(
        short EventType,
        string ChannelId,
        short Day,
        short Hour,
        DateTime UtcEventTime,
        short RepeatIntervalDays,
        string? Message,
        IReadOnlyList<ScheduledEventResponseOption>? ResponseOptions = null,
        short NotificationMinutesBeforeStart = DefaultNotificationMinutesBeforeStart);

    internal const int MaxMessageLength = 256;
    internal const short DefaultNotificationMinutesBeforeStart = 15;
    internal const short MaxNotificationMinutesBeforeStart = 10080;

    private static readonly TimeSpan AccessCacheTtl = TimeSpan.FromSeconds(60);

    private static async Task<IResult> ListGuilds(
        ClaimsPrincipal user,
        [FromServices] IUserGuildsService userGuilds,
        [FromServices] IDbContextFactory<DatabaseContext> dbContextFactory,
        [FromServices] DiscordRestClientProvider clientProvider,
        [FromServices] IMemoryCache cache)
    {
        var discordId = user.FindFirst("discord_id")?.Value;
        if (string.IsNullOrEmpty(discordId))
        {
            return Results.Unauthorized();
        }

        var cacheKey = $"scheduling-guilds:{discordId}";
        var result = await cache.GetOrCoalesceAsync(cacheKey, AccessCacheTtl, TimeSpan.FromSeconds(5), async () =>
        {
            var list = await userGuilds.GetForPrincipalAsync(user);
            if (list is null)
            {
                return [];
            }

            await using var ctx = await dbContextFactory.CreateDbContextAsync();
            var tracked = await ctx.Guild
                .Select(g => new { g.GuildId, g.GuildName, g.ScheduledEventManagerRoleIds })
                .ToListAsync();
            var trackedById = tracked.ToDictionary(g => (ulong)g.GuildId);

            if (!ulong.TryParse(discordId, out var userIdUlong))
            {
                return [];
            }

            var client = await clientProvider.GetClientAsync();
            var accessible = new List<GuildSummaryDto>();
            foreach (var ug in list)
            {
                if (!trackedById.TryGetValue(ug.Id, out var entry))
                {
                    continue;
                }

                if (UserGuildsService.HasAdministrator(ug))
                {
                    accessible.Add(new GuildSummaryDto(ug.Id.ToString(), ug.Name, UserGuildsService.BuildIconUrl(ug.Id, ug.Icon)));
                    continue;
                }

                var managerRoleIds = ParseRoleIds(entry.ScheduledEventManagerRoleIds);
                if (managerRoleIds.Count == 0)
                {
                    continue;
                }

                try
                {
                    var botGuild = await client.GetGuildAsync(ug.Id);
                    if (botGuild is null)
                    {
                        continue;
                    }
                    var member = await botGuild.GetUserAsync(userIdUlong);
                    if (member is null)
                    {
                        continue;
                    }
                    if (member.RoleIds.Any(managerRoleIds.Contains))
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
        if (!ulong.TryParse(guildId, out var guildIdUlong))
        {
            return Results.BadRequest("Invalid guild id.");
        }

        if (!await HasSchedulingAccessAsync(user, guildIdUlong, userGuilds, dbContextFactory, clientProvider))
        {
            return Results.Forbid();
        }

        var client = await clientProvider.GetClientAsync();
        var botGuild = await client.GetGuildAsync(guildIdUlong);
        if (botGuild is null)
        {
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

        return Results.Ok(new GuildContextDto(
            guildId,
            botGuild.Name,
            channels,
            roles,
            ScheduledEventResponseOptions.DefaultsForEventType((short)ScheduledEventTypeEnum.RaidSignup)));
    }

    private static async Task<IResult> ListEvents(
        string guildId,
        ClaimsPrincipal user,
        [FromServices] IUserGuildsService userGuilds,
        [FromServices] IDbContextFactory<DatabaseContext> dbContextFactory,
        [FromServices] DiscordRestClientProvider clientProvider)
    {
        if (!ulong.TryParse(guildId, out var guildIdUlong))
        {
            return Results.BadRequest("Invalid guild id.");
        }
        if (!await HasSchedulingAccessAsync(user, guildIdUlong, userGuilds, dbContextFactory, clientProvider))
        {
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
        if (!ulong.TryParse(guildId, out var guildIdUlong))
        {
            return Results.BadRequest("Invalid guild id.");
        }
        if (!await HasSchedulingAccessAsync(user, guildIdUlong, userGuilds, dbContextFactory, clientProvider))
        {
            return Results.Forbid();
        }

        var guildValidation = await GetGuildValidationContextAsync(guildIdUlong, clientProvider);
        if (guildValidation is null)
        {
            return Results.NotFound("Bot is not in that guild.");
        }

        var error = ValidateEvent(body, guildValidation.ChannelIds, guildValidation.EmojiIds);
        if (error is not null)
        {
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
            NotificationMinutesBeforeStart = body.NotificationMinutesBeforeStart,
            Message = (body.Message ?? string.Empty).Trim(),
            ResponseOptionsJson = ScheduledEventResponseOptions.SerializeForEventType(body.EventType, body.ResponseOptions),
            UtcEventTime = utc
        };
    }

    private static void ApplyEvent(ScheduledEvent entity, EventWriteDto body, string responseOptionsJson)
    {
        var utc = DateTime.SpecifyKind(body.UtcEventTime, DateTimeKind.Utc);
        entity.EventType = body.EventType;
        entity.ChannelId = long.Parse(body.ChannelId);
        entity.Day = body.Day;
        entity.Hour = body.Hour;
        entity.RepeatIntervalDays = body.RepeatIntervalDays;
        entity.NotificationMinutesBeforeStart = body.NotificationMinutesBeforeStart;
        entity.Message = (body.Message ?? string.Empty).Trim();
        entity.ResponseOptionsJson = responseOptionsJson;
        entity.UtcEventTime = utc;
    }

    private static string ResolveResponseOptionsJsonForUpdate(ScheduledEvent existing, EventWriteDto body)
    {
        if (!ScheduledEventResponseOptions.IsSignupEvent(body.EventType))
        {
            return string.Empty;
        }

        if (body.ResponseOptions is not null)
        {
            return ScheduledEventResponseOptions.SerializeForEventType(body.EventType, body.ResponseOptions);
        }

        if (ScheduledEventResponseOptions.IsSignupEvent(existing.EventType))
        {
            var existingOptions = ScheduledEventResponseOptions.ForEvent(existing.EventType, existing.ResponseOptionsJson);
            return ScheduledEventResponseOptions.Serialize(existingOptions);
        }

        return ScheduledEventResponseOptions.SerializeForEventType(body.EventType, null);
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
        if (!ulong.TryParse(guildId, out var guildIdUlong))
        {
            return Results.BadRequest("Invalid guild id.");
        }
        if (!await HasSchedulingAccessAsync(user, guildIdUlong, userGuilds, dbContextFactory, clientProvider))
        {
            return Results.Forbid();
        }

        var guildValidation = await GetGuildValidationContextAsync(guildIdUlong, clientProvider);
        if (guildValidation is null)
        {
            return Results.NotFound("Bot is not in that guild.");
        }

        var error = ValidateEvent(body, guildValidation.ChannelIds, guildValidation.EmojiIds);
        if (error is not null)
        {
            return Results.BadRequest(error);
        }

        var guildIdLong = (long)guildIdUlong;
        await using var ctx = await dbContextFactory.CreateDbContextAsync();
        var existing = await ctx.ScheduledEvent.AsTracking()
            .FirstOrDefaultAsync(e => e.ScheduledEventId == eventId && e.GuildId == guildIdLong);
        if (existing is null)
        {
            return Results.NotFound();
        }

        var prior = ScheduledEventUpdateSnapshot.Capture(existing);

        var responseOptionsJson = ResolveResponseOptionsJsonForUpdate(existing, body);
        ApplyEvent(existing, body, responseOptionsJson);
        var newIsSignup = ScheduledEventResponseOptions.IsSignupEvent(existing.EventType);

        if (prior.IsPostedSignup)
        {
            var syncError = await SaveAndSyncPostedSignupMessageForUpdateAsync(
                ctx,
                clientProvider,
                guildIdUlong,
                existing,
                prior,
                newIsSignup);

            if (syncError is not null)
            {
                return Results.Problem(syncError, statusCode: StatusCodes.Status502BadGateway);
            }
        }
        else
        {
            await ctx.SaveChangesAsync();
        }

        return Results.Ok(ToDto(existing));
    }

    private static async Task<string?> SaveAndSyncPostedSignupMessageForUpdateAsync(
        DatabaseContext ctx,
        DiscordRestClientProvider clientProvider,
        ulong guildId,
        ScheduledEvent scheduledEvent,
        ScheduledEventUpdateSnapshot prior,
        bool newIsSignup)
    {
        var priorChannelId = (ulong)prior.ChannelId;
        var priorMessageId = (ulong)prior.MessageId!.Value;

        if (!newIsSignup)
        {
            scheduledEvent.MessageId = null;
            scheduledEvent.PostedEventTime = null;
            await ctx.SaveChangesAsync();

            if (!await TryDeletePostedMessageAsync(clientProvider, guildId, priorChannelId, priorMessageId))
            {
                await TryRestoreEventAsync(ctx, scheduledEvent, prior);
                return "Failed to remove the existing signup message. The scheduled event was not updated.";
            }

            return null;
        }

        var newChannelId = (ulong)scheduledEvent.ChannelId;
        if (newChannelId == priorChannelId)
        {
            scheduledEvent.MessageId = prior.MessageId;
            await ctx.SaveChangesAsync();

            if (!await TryUpdatePostedMessageAsync(clientProvider, guildId, priorChannelId, priorMessageId, scheduledEvent))
            {
                await TryRestoreEventAsync(ctx, scheduledEvent, prior);
                return "Failed to update the existing signup message. The scheduled event was not updated.";
            }

            return null;
        }

        var newMessageId = await TryPostSignupMessageAsync(clientProvider, guildId, newChannelId, scheduledEvent);
        if (newMessageId is null)
        {
            return "Failed to post the signup message in the new channel. The scheduled event was not updated.";
        }

        scheduledEvent.MessageId = (long)newMessageId.Value;

        try
        {
            await ctx.SaveChangesAsync();
        }
        catch
        {
            await TryDeletePostedMessageAsync(clientProvider, guildId, newChannelId, newMessageId.Value);
            throw;
        }

        if (!await TryDeletePostedMessageAsync(clientProvider, guildId, priorChannelId, priorMessageId))
        {
            if (await TryRestoreEventAsync(ctx, scheduledEvent, prior))
            {
                await TryDeletePostedMessageAsync(clientProvider, guildId, newChannelId, newMessageId.Value);
            }

            return "Failed to remove the previous signup message. The scheduled event was not updated.";
        }

        return null;
    }

    private static async Task<bool> TryRestoreEventAsync(
        DatabaseContext ctx,
        ScheduledEvent scheduledEvent,
        ScheduledEventUpdateSnapshot prior)
    {
        try
        {
            prior.ApplyTo(scheduledEvent);
            await ctx.SaveChangesAsync();
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static async Task<bool> TryUpdatePostedMessageAsync(
        DiscordRestClientProvider clientProvider,
        ulong guildId,
        ulong channelId,
        ulong messageId,
        ScheduledEvent scheduledEvent)
    {
        try
        {
            var client = await clientProvider.GetClientAsync();
            var botGuild = await client.GetGuildAsync(guildId);
            if (botGuild is null)
            {
                return false;
            }
            var channel = await botGuild.GetTextChannelAsync(channelId);
            if (channel is null)
            {
                return false;
            }
            var message = await channel.GetMessageAsync(messageId);
            if (message is not Discord.Rest.RestUserMessage userMessage)
            {
                return false;
            }

            var existingEmbed = userMessage.Embeds.FirstOrDefault();
            await userMessage.ModifyAsync(p =>
            {
                p.Content = SignupMessageBuilder.BuildContent(scheduledEvent);
                p.Embed = SignupMessageBuilder.BuildEmbed(scheduledEvent, existingEmbed);
                p.Components = SignupMessageBuilder.BuildComponents(scheduledEvent);
            });
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static async Task<ulong?> TryPostSignupMessageAsync(
        DiscordRestClientProvider clientProvider,
        ulong guildId,
        ulong channelId,
        ScheduledEvent scheduledEvent)
    {
        try
        {
            var client = await clientProvider.GetClientAsync();
            var botGuild = await client.GetGuildAsync(guildId);
            if (botGuild is null)
            {
                return null;
            }
            var channel = await botGuild.GetTextChannelAsync(channelId);
            if (channel is null)
            {
                return null;
            }

            var message = await channel.SendMessageAsync(
                text: SignupMessageBuilder.BuildContent(scheduledEvent),
                embed: SignupMessageBuilder.BuildEmbed(scheduledEvent),
                components: SignupMessageBuilder.BuildComponents(scheduledEvent));

            return message.Id;
        }
        catch
        {
            return null;
        }
    }

    private static async Task<bool> TryDeletePostedMessageAsync(
        DiscordRestClientProvider clientProvider,
        ulong guildId,
        ulong channelId,
        ulong messageId)
    {
        try
        {
            var client = await clientProvider.GetClientAsync();
            var botGuild = await client.GetGuildAsync(guildId);
            if (botGuild is null)
            {
                return false;
            }
            var channel = await botGuild.GetTextChannelAsync(channelId);
            if (channel is null)
            {
                return false;
            }
            var message = await channel.GetMessageAsync(messageId);
            if (message is null)
            {
                return true;
            }

            await message.DeleteAsync();
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static async Task<IResult> DeleteEvent(
        string guildId,
        long eventId,
        ClaimsPrincipal user,
        [FromServices] IUserGuildsService userGuilds,
        [FromServices] IDbContextFactory<DatabaseContext> dbContextFactory,
        [FromServices] DiscordRestClientProvider clientProvider,
        [FromServices] ILoggerFactory loggerFactory)
    {
        if (!ulong.TryParse(guildId, out var guildIdUlong))
        {
            return Results.BadRequest("Invalid guild id.");
        }
        if (!await HasSchedulingAccessAsync(user, guildIdUlong, userGuilds, dbContextFactory, clientProvider))
        {
            return Results.Forbid();
        }

        var guildIdLong = (long)guildIdUlong;
        await using var ctx = await dbContextFactory.CreateDbContextAsync();
        var existing = await ctx.ScheduledEvent.AsTracking()
            .FirstOrDefaultAsync(e => e.ScheduledEventId == eventId && e.GuildId == guildIdLong);
        if (existing is null)
        {
            return Results.NotFound();
        }

        var prior = ScheduledEventUpdateSnapshot.Capture(existing);
        var shouldDeletePostedSignup = prior.IsPostedSignup;

        ctx.ScheduledEvent.Remove(existing);
        await ctx.SaveChangesAsync();

        if (shouldDeletePostedSignup)
        {
            if (!await TryDeletePostedMessageAsync(
                clientProvider,
                guildIdUlong,
                (ulong)prior.ChannelId,
                (ulong)prior.MessageId!.Value))
            {
                await TryReinsertEventAsync(ctx, existing, loggerFactory.CreateLogger("DonBot.Api.Endpoints.SchedulingEndpoints"));
                return Results.Problem(
                    "Failed to remove the existing signup message. The scheduled event was not deleted.",
                    statusCode: StatusCodes.Status502BadGateway);
            }
        }

        return Results.NoContent();
    }

    private static async Task TryReinsertEventAsync(
        DatabaseContext ctx,
        ScheduledEvent scheduledEvent,
        ILogger logger)
    {
        try
        {
            ctx.ScheduledEvent.Add(scheduledEvent);
            await ctx.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                ex,
                "Failed to reinsert scheduled event {ScheduledEventId} after posted message deletion failed.",
                scheduledEvent.ScheduledEventId);
        }
    }

    private static async Task<GuildValidationContext?> GetGuildValidationContextAsync(
        ulong guildId, DiscordRestClientProvider clientProvider)
    {
        var client = await clientProvider.GetClientAsync();
        var botGuild = await client.GetGuildAsync(guildId);
        if (botGuild is null)
        {
            return null;
        }
        var channels = (await botGuild.GetTextChannelsAsync()).Select(c => c.Id).ToHashSet();
        var emotes = (await botGuild.GetEmotesAsync()).Select(e => e.Id).ToHashSet();
        return new GuildValidationContext(channels, emotes);
    }

    internal static string? ValidateEvent(
        EventWriteDto body,
        HashSet<ulong> validChannelIds,
        HashSet<ulong>? validCustomEmojiIds = null)
    {
        if (!Enum.IsDefined(typeof(ScheduledEventTypeEnum), body.EventType))
        {
            return "Invalid event type.";
        }
        if (body.EventType == 3)
        {
            return "Invalid event type.";
        }
        if (body.EventType == (short)ScheduledEventTypeEnum.WvwRaidSignup)
        {
            return "WvW raid signup has been consolidated into raid signup. Use response options instead.";
        }
        if (body.Day < 0 || body.Day > 6)
        {
            return "Post day must be 0-6 (Sunday-Saturday).";
        }
        if (body.Hour < 0 || body.Hour > 23)
        {
            return "Post hour must be 0-23.";
        }
        if (body.UtcEventTime == default)
        {
            return "Event time is required.";
        }
        if (body.UtcEventTime <= DateTime.UtcNow)
        {
            return "Event time must be in the future.";
        }
        if (body.RepeatIntervalDays < 1 || body.RepeatIntervalDays > 365)
        {
            return "Repeat interval must be 1-365 days.";
        }
        if (body.NotificationMinutesBeforeStart < 1 || body.NotificationMinutesBeforeStart > MaxNotificationMinutesBeforeStart)
        {
            return $"Notification lead time must be 1-{MaxNotificationMinutesBeforeStart} minutes.";
        }
        if (!ulong.TryParse(body.ChannelId, out var channelParsed) || !validChannelIds.Contains(channelParsed))
        {
            return "Channel does not belong to this guild.";
        }
        if (!string.IsNullOrEmpty(body.Message) && body.Message.Length > MaxMessageLength)
        {
            return $"Message must be {MaxMessageLength} characters or fewer.";
        }
        var responseOptionsError = ScheduledEventResponseOptions.ValidateForEventType(body.EventType, body.ResponseOptions);
        if (responseOptionsError is not null)
        {
            return responseOptionsError;
        }
        if (ScheduledEventResponseOptions.IsSignupEvent(body.EventType) && body.ResponseOptions is not null)
        {
            var responseOptions = ScheduledEventResponseOptions.Normalize(body.ResponseOptions);
            foreach (var responseOption in responseOptions)
            {
                if (!TryParseResponseEmoji(responseOption.Emoji ?? string.Empty, validCustomEmojiIds))
                {
                    return "Response option emojis must be valid Unicode or server custom emojis.";
                }
            }
        }
        return null;
    }

    private static bool TryParseResponseEmoji(string emoji, HashSet<ulong>? validCustomEmojiIds)
    {
        try
        {
            _ = Emoji.Parse(emoji);
            return true;
        }
        catch (Exception)
        {
            // Try custom Discord emotes below.
        }

        try
        {
            var emote = Emote.Parse(emoji);
            return validCustomEmojiIds is null || validCustomEmojiIds.Contains(emote.Id);
        }
        catch (Exception)
        {
            return false;
        }
    }

    internal static EventDto ToDto(ScheduledEvent e)
    {
        var responseOptions = ScheduledEventResponseOptions.ForEvent(e.EventType, e.ResponseOptionsJson);
        var eventType = ScheduledEventResponseOptions.ToCurrentEventType(e.EventType);

        return new EventDto(
            e.ScheduledEventId,
            eventType,
            e.ChannelId.ToString(),
            e.Day,
            e.Hour,
            e.RepeatIntervalDays,
            e.Message,
            responseOptions,
            e.UtcEventTime,
            e.NotificationMinutesBeforeStart);
    }

    internal static HashSet<ulong> ParseRoleIds(string? csv)
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

    internal static async Task<bool> HasSchedulingAccessAsync(
        ClaimsPrincipal user,
        ulong guildId,
        IUserGuildsService userGuilds,
        IDbContextFactory<DatabaseContext> dbContextFactory,
        DiscordRestClientProvider clientProvider)
    {
        if (await userGuilds.HasAdministratorAsync(user, guildId))
        {
            return true;
        }
        if (!await userGuilds.IsMemberAsync(user, guildId))
        {
            return false;
        }

        var discordIdRaw = user.FindFirst("discord_id")?.Value;
        if (!ulong.TryParse(discordIdRaw, out var userIdUlong))
        {
            return false;
        }

        var guildIdLong = (long)guildId;
        await using var ctx = await dbContextFactory.CreateDbContextAsync();
        var managerCsv = await ctx.Guild
            .Where(g => g.GuildId == guildIdLong)
            .Select(g => g.ScheduledEventManagerRoleIds)
            .FirstOrDefaultAsync();
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
            var member = await botGuild.GetUserAsync(userIdUlong);
            if (member is null)
            {
                return false;
            }
            return member.RoleIds.Any(managerRoles.Contains);
        }
        catch
        {
            return false;
        }
    }

    // ASP.NET Core and System.Text.Json use these response DTO members implicitly.
    // ReSharper disable UnusedAutoPropertyAccessor.Local
    // ReSharper disable UnusedMember.Local
    private sealed class GuildSummaryDto(string guildId, string name, string? iconUrl)
    {
        public string GuildId { get; } = guildId;

        public string Name { get; } = name;

        public string? IconUrl { get; } = iconUrl;
    }

    private sealed class ChannelDto(string id, string name)
    {
        public string Id { get; } = id;

        public string Name { get; } = name;
    }

    private sealed class RoleDto(string id, string name)
    {
        public string Id { get; } = id;

        public string Name { get; } = name;
    }

    private sealed class GuildContextDto(
        string guildId,
        string guildName,
        IReadOnlyList<ChannelDto> channels,
        IReadOnlyList<RoleDto> roles,
        IReadOnlyList<ScheduledEventResponseOption> defaultSignupResponseOptions)
    {
        public string GuildId { get; } = guildId;

        public string GuildName { get; } = guildName;

        public IReadOnlyList<ChannelDto> Channels { get; } = channels;

        public IReadOnlyList<RoleDto> Roles { get; } = roles;

        public IReadOnlyList<ScheduledEventResponseOption> DefaultSignupResponseOptions { get; } = defaultSignupResponseOptions;
    }
    // ReSharper restore UnusedAutoPropertyAccessor.Local
    // ReSharper restore UnusedMember.Local

    private sealed record GuildValidationContext(HashSet<ulong> ChannelIds, HashSet<ulong> EmojiIds);

    private sealed record ScheduledEventUpdateSnapshot(
        long GuildId,
        short EventType,
        long ChannelId,
        long? MessageId,
        DateTime? PostedEventTime,
        DateTime? LastNotificationEventTime,
        short Day,
        short Hour,
        DateTime UtcEventTime,
        short RepeatIntervalDays,
        short NotificationMinutesBeforeStart,
        string Message,
        string ResponseOptionsJson)
    {
        public static ScheduledEventUpdateSnapshot Capture(ScheduledEvent scheduledEvent) =>
            new(
                scheduledEvent.GuildId,
                scheduledEvent.EventType,
                scheduledEvent.ChannelId,
                scheduledEvent.MessageId,
                scheduledEvent.PostedEventTime,
                scheduledEvent.LastNotificationEventTime,
                scheduledEvent.Day,
                scheduledEvent.Hour,
                scheduledEvent.UtcEventTime,
                scheduledEvent.RepeatIntervalDays,
                scheduledEvent.NotificationMinutesBeforeStart,
                scheduledEvent.Message,
                scheduledEvent.ResponseOptionsJson);

        public bool IsPostedSignup =>
            ScheduledEventResponseOptions.IsSignupEvent(EventType) && MessageId.HasValue;

        public void ApplyTo(ScheduledEvent scheduledEvent)
        {
            scheduledEvent.GuildId = GuildId;
            scheduledEvent.EventType = EventType;
            scheduledEvent.ChannelId = ChannelId;
            scheduledEvent.MessageId = MessageId;
            scheduledEvent.PostedEventTime = PostedEventTime;
            scheduledEvent.LastNotificationEventTime = LastNotificationEventTime;
            scheduledEvent.Day = Day;
            scheduledEvent.Hour = Hour;
            scheduledEvent.UtcEventTime = UtcEventTime;
            scheduledEvent.RepeatIntervalDays = RepeatIntervalDays;
            scheduledEvent.NotificationMinutesBeforeStart = NotificationMinutesBeforeStart;
            scheduledEvent.Message = Message;
            scheduledEvent.ResponseOptionsJson = ResponseOptionsJson;
        }
    }
}
