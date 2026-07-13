using System.Security.Claims;
using Discord;
using DonBot.Api.Services;
using DonBot.Core.Models.Entities;
using DonBot.Core.Models.Enums;
using DonBot.Core.Models.Scheduling;
using DonBot.Core.Services.Scheduling;
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

    internal const int MaxMessageLength = ScheduledEventRules.MaxMessageLength;
    internal const short DefaultNotificationMinutesBeforeStart = ScheduledEventRules.DefaultNotificationMinutesBeforeStart;
    internal const short MaxNotificationMinutesBeforeStart = ScheduledEventRules.MaxNotificationMinutesBeforeStart;

    private static readonly TimeSpan AccessCacheTtl = TimeSpan.FromSeconds(60);

    private static async Task<IResult> ListGuilds(
        ClaimsPrincipal user,
        [FromServices] IDbContextFactory<DatabaseContext> dbContextFactory,
        [FromServices] DiscordRestClientProvider clientProvider,
        [FromServices] AccessibleGuildsCache accessibleGuildsCache,
        CancellationToken ct)
    {
        var result = await accessibleGuildsCache.GetAsync<GuildSummaryDto>(
            user,
            "scheduling-guilds",
            AccessCacheTtl,
            TimeSpan.FromSeconds(5),
            async (list, cancellationToken) =>
        {
            await using var ctx = await dbContextFactory.CreateDbContextAsync(cancellationToken);
            var tracked = await ctx.Guild
                .Select(g => new { g.GuildId, g.GuildName, g.ScheduledEventManagerRoleIds })
                .ToListAsync(cancellationToken);
            var trackedById = tracked
                .Where(g => g.GuildId > 0)
                .ToDictionary(g => (ulong)g.GuildId);

            if (!ulong.TryParse(user.FindFirst("discord_id")?.Value, out var userIdUlong))
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
        },
            ct);

        if (result.IsUnauthorized)
        {
            return Results.Unauthorized();
        }

        return Results.Ok(result.Guilds);
    }

    private static async Task<IResult> GetGuildContext(
        string guildId,
        ClaimsPrincipal user,
        [FromServices] GuildAccessGuard accessGuard,
        [FromServices] IDbContextFactory<DatabaseContext> dbContextFactory,
        [FromServices] DiscordRestClientProvider clientProvider)
    {
        if (!GuildRouteParser.TryParse(guildId, out var guildRoute))
        {
            return Results.BadRequest("Invalid guild id.");
        }

        if (await accessGuard.RequireSchedulingAccessAsync(user, guildRoute) is { } denied)
        {
            return denied;
        }

        var client = await clientProvider.GetClientAsync();
        var botGuild = await client.GetGuildAsync(guildRoute.UnsignedValue);
        if (botGuild is null)
        {
            return Results.NotFound("Bot is not in that guild.");
        }

        var channels = (await botGuild.GetTextChannelsAsync())
            .OrderBy(c => c.Position)
            .Select(c => new ChannelDto(c.Id.ToString(), c.Name))
            .ToList();

        var roles = botGuild.Roles
            .Where(r => r.Id != botGuild.EveryoneRole.Id)
            .OrderByDescending(r => r.Position)
            .Select(r => new RoleDto(r.Id.ToString(), r.Name))
            .ToList();

        return Results.Ok(new GuildContextDto(
            guildId,
            botGuild.Name,
            channels,
            roles,
            ScheduledEventResponseOptions.DefaultsForEventType((short)ScheduledEventTypeEnum.RaidSignup),
            ScheduledEventRules.GetFormMetadata()));
    }

    private static async Task<IResult> ListEvents(
        string guildId,
        ClaimsPrincipal user,
        [FromServices] GuildAccessGuard accessGuard,
        [FromServices] IDbContextFactory<DatabaseContext> dbContextFactory,
        [FromServices] DiscordRestClientProvider clientProvider)
    {
        if (!GuildRouteParser.TryParse(guildId, out var guildRoute))
        {
            return Results.BadRequest("Invalid guild id.");
        }
        if (await accessGuard.RequireSchedulingAccessAsync(user, guildRoute) is { } denied)
        {
            return denied;
        }

        await using var ctx = await dbContextFactory.CreateDbContextAsync();
        var events = await ctx.ScheduledEvent
            .Where(e => e.GuildId == guildRoute.Value)
            .OrderBy(e => e.UtcEventTime)
            .ToListAsync();

        return Results.Ok(events.Select(ToDto).ToList());
    }

    private static async Task<IResult> CreateEvent(
        string guildId,
        EventWriteDto body,
        ClaimsPrincipal user,
        [FromServices] GuildAccessGuard accessGuard,
        [FromServices] IDbContextFactory<DatabaseContext> dbContextFactory,
        [FromServices] DiscordRestClientProvider clientProvider)
    {
        if (!GuildRouteParser.TryParse(guildId, out var guildRoute))
        {
            return Results.BadRequest("Invalid guild id.");
        }
        if (await accessGuard.RequireSchedulingAccessAsync(user, guildRoute) is { } denied)
        {
            return denied;
        }

        var guildValidation = await GetGuildValidationContextAsync(guildRoute.UnsignedValue, clientProvider);
        if (guildValidation is null)
        {
            return Results.NotFound("Bot is not in that guild.");
        }

        var error = ValidateEvent(
            body,
            guildValidation.ChannelIds,
            guildValidation.EmojiIds,
            guildValidation.RoleIds,
            out var channelId);
        if (error is not null)
        {
            return Results.BadRequest(error);
        }

        var entity = ScheduledEventPlanner.BuildEvent(guildRoute.Value, ToCoreWriteRequest(body, channelId));

        await using var ctx = await dbContextFactory.CreateDbContextAsync();
        ctx.ScheduledEvent.Add(entity);
        await ctx.SaveChangesAsync();
        return Results.Ok(ToDto(entity));
    }

    private static async Task<IResult> UpdateEvent(
        string guildId,
        long eventId,
        EventWriteDto body,
        ClaimsPrincipal user,
        [FromServices] GuildAccessGuard accessGuard,
        [FromServices] IDbContextFactory<DatabaseContext> dbContextFactory,
        [FromServices] DiscordRestClientProvider clientProvider)
    {
        if (!GuildRouteParser.TryParse(guildId, out var guildRoute))
        {
            return Results.BadRequest("Invalid guild id.");
        }
        if (await accessGuard.RequireSchedulingAccessAsync(user, guildRoute) is { } denied)
        {
            return denied;
        }

        var guildValidation = await GetGuildValidationContextAsync(guildRoute.UnsignedValue, clientProvider);
        if (guildValidation is null)
        {
            return Results.NotFound("Bot is not in that guild.");
        }

        var error = ValidateEvent(
            body,
            guildValidation.ChannelIds,
            guildValidation.EmojiIds,
            guildValidation.RoleIds,
            out var channelId);
        if (error is not null)
        {
            return Results.BadRequest(error);
        }

        await using var ctx = await dbContextFactory.CreateDbContextAsync();
        var existing = await ctx.ScheduledEvent.AsTracking()
            .FirstOrDefaultAsync(e => e.ScheduledEventId == eventId && e.GuildId == guildRoute.Value);
        if (existing is null)
        {
            return Results.NotFound();
        }

        var prior = ScheduledEventSnapshot.Capture(existing);

        ScheduledEventPlanner.ApplyForUpdate(existing, ToCoreWriteRequest(body, channelId));
        var newIsSignup = ScheduledEventResponseOptions.IsSignupEvent(existing.EventType);

        if (prior.IsPostedSignup)
        {
            var syncError = await SaveAndSyncPostedSignupMessageForUpdateAsync(
                ctx,
                clientProvider,
                guildRoute.UnsignedValue,
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
        ScheduledEventSnapshot prior,
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
        ScheduledEventSnapshot prior)
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
        [FromServices] GuildAccessGuard accessGuard,
        [FromServices] IDbContextFactory<DatabaseContext> dbContextFactory,
        [FromServices] DiscordRestClientProvider clientProvider,
        [FromServices] ILoggerFactory loggerFactory)
    {
        if (!GuildRouteParser.TryParse(guildId, out var guildRoute))
        {
            return Results.BadRequest("Invalid guild id.");
        }
        if (await accessGuard.RequireSchedulingAccessAsync(user, guildRoute) is { } denied)
        {
            return denied;
        }

        await using var ctx = await dbContextFactory.CreateDbContextAsync();
        var existing = await ctx.ScheduledEvent.AsTracking()
            .FirstOrDefaultAsync(e => e.ScheduledEventId == eventId && e.GuildId == guildRoute.Value);
        if (existing is null)
        {
            return Results.NotFound();
        }

        var prior = ScheduledEventSnapshot.Capture(existing);
        var shouldDeletePostedSignup = prior.IsPostedSignup;

        ctx.ScheduledEvent.Remove(existing);
        await ctx.SaveChangesAsync();

        if (shouldDeletePostedSignup)
        {
            if (!await TryDeletePostedMessageAsync(
                clientProvider,
                guildRoute.UnsignedValue,
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
        var roles = botGuild.Roles
            .Where(r => r.Id != botGuild.EveryoneRole.Id)
            .Select(r => r.Id)
            .ToHashSet();
        return new GuildValidationContext(channels, emotes, roles);
    }

    internal static string? ValidateEvent(
        EventWriteDto body,
        HashSet<ulong> validChannelIds,
        HashSet<ulong>? validCustomEmojiIds = null,
        HashSet<ulong>? validRoleIds = null) =>
        ValidateEvent(body, validChannelIds, validCustomEmojiIds, validRoleIds, out _);

    private static string? ValidateEvent(
        EventWriteDto body,
        HashSet<ulong> validChannelIds,
        HashSet<ulong>? validCustomEmojiIds,
        HashSet<ulong>? validRoleIds,
        out long channelId)
    {
        channelId = 0;

        var coreRequest = ToCoreWriteRequest(body, channelId: 0);
        var coreError = ScheduledEventRules.ValidateScheduleFields(coreRequest);
        if (coreError is not null)
        {
            return coreError;
        }

        if (!long.TryParse(body.ChannelId, out channelId)
            || channelId <= 0
            || !validChannelIds.Contains((ulong)channelId))
        {
            channelId = 0;
            return "Channel does not belong to this guild.";
        }

        coreError = ScheduledEventRules.ValidateContentFields(coreRequest);
        if (coreError is not null)
        {
            return coreError;
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

                if (validRoleIds is not null
                    && responseOption.AllowedRoleIds?.Any(roleId =>
                        !ulong.TryParse(roleId, out var parsedRoleId)
                        || !validRoleIds.Contains(parsedRoleId)) == true)
                {
                    return "Allowed response roles must belong to this guild.";
                }
            }
        }
        return null;
    }

    private static ScheduledEventWriteRequest ToCoreWriteRequest(EventWriteDto body, long channelId) =>
        new(
            body.EventType,
            channelId,
            body.Day,
            body.Hour,
            body.UtcEventTime,
            body.RepeatIntervalDays,
            body.Message,
            body.ResponseOptions,
            body.NotificationMinutesBeforeStart);

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

    internal static HashSet<ulong> ParseRoleIds(string? csv) => GuildAccessGuard.ParseRoleIds(csv);

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
        IReadOnlyList<ScheduledEventResponseOption> defaultSignupResponseOptions,
        ScheduledEventFormMetadata scheduling)
    {
        public string GuildId { get; } = guildId;

        public string GuildName { get; } = guildName;

        public IReadOnlyList<ChannelDto> Channels { get; } = channels;

        public IReadOnlyList<RoleDto> Roles { get; } = roles;

        public IReadOnlyList<ScheduledEventResponseOption> DefaultSignupResponseOptions { get; } = defaultSignupResponseOptions;

        public ScheduledEventFormMetadata Scheduling { get; } = scheduling;
    }
    // ReSharper restore UnusedAutoPropertyAccessor.Local
    // ReSharper restore UnusedMember.Local

    private sealed record GuildValidationContext(
        HashSet<ulong> ChannelIds,
        HashSet<ulong> EmojiIds,
        HashSet<ulong> RoleIds);

}
