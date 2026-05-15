using System.Security.Claims;
using Discord;
using Discord.Rest;
using DonBot.Api.Services;
using DonBot.Models.Apis.GuildWars2Api;
using DonBot.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace DonBot.Api.Endpoints;

public static class GuildAdminEndpoints
{
    public static void MapGuildAdminEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/admin").RequireAuthorization();
        group.MapGet("/guilds", ListAdminGuilds);
        group.MapGet("/guilds/{guildId}/config", GetGuildConfig);
        group.MapPut("/guilds/{guildId}/config", UpdateGuildConfig);
        group.MapGet("/gw2/my-guilds", GetMyGw2Guilds);
        group.MapGet("/gw2/search", SearchGw2Guilds);
        group.MapGet("/guilds/{guildId}/quotes", ListQuotes);
        group.MapPost("/guilds/{guildId}/quotes", CreateQuote);
        group.MapPut("/guilds/{guildId}/quotes/{quoteId}", UpdateQuote);
        group.MapDelete("/guilds/{guildId}/quotes/{quoteId}", DeleteQuote);
    }

    public record QuoteDto(long QuoteId, string Quote);

    public record QuoteWriteDto(string Quote);

    internal const int MaxQuoteLength = 1000;

    public record Gw2GuildDto(string Id, string Name, string? Tag);

    public record MyGw2GuildsResponse(bool HasAccount, IReadOnlyList<Gw2GuildDto> Guilds);

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
        IReadOnlyList<RoleDto> Roles,
        IReadOnlyList<Gw2GuildDto> Gw2GuildNames);

    private static readonly TimeSpan AdminGuildsCacheTtl = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan Gw2GuildNameCacheTtl = TimeSpan.FromHours(24);

    private static async Task<IResult> ListAdminGuilds(
        ClaimsPrincipal user,
        IUserGuildsService userGuilds,
        IDbContextFactory<DatabaseContext> dbContextFactory,
        IMemoryCache cache)
    {
        var discordId = user.FindFirst("discord_id")?.Value;
        if (string.IsNullOrEmpty(discordId)) {
            return Results.Unauthorized();
        }

        var cacheKey = $"admin-guilds:{discordId}";
        var cached = await cache.GetOrCoalesceAsync(cacheKey, AdminGuildsCacheTtl, TimeSpan.FromSeconds(10), async () =>
        {
            var userGuildList = await userGuilds.GetForPrincipalAsync(user);
            if (userGuildList is null) {
                return new List<GuildSummaryDto>();
            }

            await using var context = await dbContextFactory.CreateDbContextAsync();
            var trackedSet = (await context.Guild.Select(g => g.GuildId).ToListAsync())
                .Select(id => (ulong)id).ToHashSet();

            return userGuildList
                .Where(g => trackedSet.Contains(g.Id) && UserGuildsService.HasAdministrator(g))
                .Select(g => new GuildSummaryDto(g.Id.ToString(), g.Name, UserGuildsService.BuildIconUrl(g.Id, g.Icon)))
                .OrderBy(g => g.Name)
                .ToList();
        });

        return Results.Ok(cached);
    }

    private static async Task<IResult> GetGuildConfig(
        string guildId,
        ClaimsPrincipal user,
        DiscordRestClientProvider clientProvider,
        IUserGuildsService userGuilds,
        IDbContextFactory<DatabaseContext> dbContextFactory,
        IMemoryCache cache,
        IHttpClientFactory httpClientFactory,
        ILogger<DiscordRestClientProvider> logger)
    {
        if (!ulong.TryParse(guildId, out var guildIdUlong)) {
            return Results.BadRequest("Invalid guild id.");
        }
        var guildIdLong = (long)guildIdUlong;

        // Run independent work in parallel: admin check (OAuth, cached), Discord client, DB read.
        var clientTask = clientProvider.GetClientAsync();
        var adminTask = userGuilds.HasAdministratorAsync(user, guildIdUlong);
        var dbReadTask = ReadGuildAsync(dbContextFactory, guildIdLong);

        if (!await adminTask) {
            return Results.Forbid();
        }

        var client = await clientTask;
        var botGuild = await client.GetGuildAsync(guildIdUlong);
        if (botGuild is null) {
            return Results.NotFound("Bot is not in that guild.");
        }

        var channelsTask = botGuild.GetTextChannelsAsync();
        var existingGuild = await dbReadTask;

        var guild = existingGuild;
        if (guild is null)
        {
            await using var writeCtx = await dbContextFactory.CreateDbContextAsync();
            guild = new Guild { GuildId = guildIdLong, GuildName = botGuild.Name };
            writeCtx.Guild.Add(guild);
            await writeCtx.SaveChangesAsync();
        }

        var dto = ToDto(guild);
        var gw2NamesTask = ResolveGw2GuildNamesAsync(CollectGw2GuildIds(dto), cache, httpClientFactory, logger);

        var channels = (await channelsTask)
            .OrderBy(c => c.Position)
            .Select(c => new ChannelDto(c.Id.ToString(), c.Name))
            .ToList();

        var roles = botGuild.Roles
            .Where(r => r.Id != botGuild.EveryoneRole.Id && !r.IsManaged)
            .OrderByDescending(r => r.Position)
            .Select(r => new RoleDto(r.Id.ToString(), r.Name))
            .ToList();

        var gw2Names = await gw2NamesTask;

        var response = new GuildConfigResponse(
            guildId,
            botGuild.Name,
            dto,
            channels,
            roles,
            gw2Names);

        return Results.Ok(response);
    }

    private static async Task<Guild?> ReadGuildAsync(IDbContextFactory<DatabaseContext> dbContextFactory, long guildIdLong)
    {
        await using var ctx = await dbContextFactory.CreateDbContextAsync();
        return await ctx.Guild.FirstOrDefaultAsync(g => g.GuildId == guildIdLong);
    }

    private static async Task<IResult> UpdateGuildConfig(
        string guildId,
        GuildConfigDto dto,
        ClaimsPrincipal user,
        DiscordRestClientProvider clientProvider,
        IUserGuildsService userGuilds,
        IDbContextFactory<DatabaseContext> dbContextFactory,
        IMemoryCache cache,
        IHttpClientFactory httpClientFactory,
        ILogger<DiscordRestClientProvider> logger)
    {
        if (!ulong.TryParse(guildId, out var guildIdUlong)) {
            return Results.BadRequest("Invalid guild id.");
        }

        if (!await userGuilds.HasAdministratorAsync(user, guildIdUlong)) {
            return Results.Forbid();
        }

        var client = await clientProvider.GetClientAsync();
        var botGuild = await client.GetGuildAsync(guildIdUlong);
        if (botGuild is null) {
            return Results.NotFound("Bot is not in that guild.");
        }

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

        if (channelError is not null) {
            return Results.BadRequest(channelError);
        }

        string? roleError = ValidateOptionalSnowflake(dto.DiscordGuildMemberRoleId, validRoleIds, nameof(dto.DiscordGuildMemberRoleId))
            ?? ValidateOptionalSnowflake(dto.DiscordSecondaryMemberRoleId, validRoleIds, nameof(dto.DiscordSecondaryMemberRoleId))
            ?? ValidateOptionalSnowflake(dto.DiscordVerifiedRoleId, validRoleIds, nameof(dto.DiscordVerifiedRoleId));

        if (roleError is not null) {
            return Results.BadRequest(roleError);
        }

        var gw2Ids = CollectGw2GuildIds(dto);
        var gw2Names = await ResolveGw2GuildNamesAsync(gw2Ids, cache, httpClientFactory, logger);
        var unresolved = gw2Ids.Where(id => !gw2Names.Any(n => n.Id == id)).ToList();
        if (unresolved.Count > 0) {
            return Results.BadRequest($"Unknown GW2 guild id(s): {string.Join(", ", unresolved)}.");
        }

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

    private static async Task<IResult> GetMyGw2Guilds(
        ClaimsPrincipal user,
        IDbContextFactory<DatabaseContext> dbContextFactory,
        IMemoryCache cache,
        IHttpClientFactory httpClientFactory,
        ILogger<DiscordRestClientProvider> logger)
    {
        if (!TryGetDiscordId(user, out var discordIdUlong)) {
            return Results.Unauthorized();
        }
        var discordId = (long)discordIdUlong;

        await using var context = await dbContextFactory.CreateDbContextAsync();

        var accounts = await context.GuildWarsAccount
            .Where(g => g.DiscordId == discordId)
            .Select(g => g.GuildWarsGuilds)
            .ToListAsync();

        if (accounts.Count == 0) {
            return Results.Ok(new MyGw2GuildsResponse(false, []));
        }

        var ids = accounts
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .SelectMany(s => s!.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Distinct()
            .ToList();

        var resolved = await ResolveGw2GuildNamesAsync(ids, cache, httpClientFactory, logger);
        var ordered = resolved.OrderBy(g => g.Name, StringComparer.OrdinalIgnoreCase).ToList();
        return Results.Ok(new MyGw2GuildsResponse(true, ordered));
    }

    internal static List<string> CollectGw2GuildIds(GuildConfigDto dto)
    {
        var ids = new List<string>();
        if (!string.IsNullOrWhiteSpace(dto.Gw2GuildMemberRoleId)) {
            ids.Add(dto.Gw2GuildMemberRoleId.Trim());
        }
        if (!string.IsNullOrWhiteSpace(dto.Gw2SecondaryMemberRoleIds)) {
            ids.AddRange(dto.Gw2SecondaryMemberRoleIds
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        }
        return ids.Distinct().ToList();
    }

    private static async Task<IResult> SearchGw2Guilds(
        string? name,
        IMemoryCache cache,
        IHttpClientFactory httpClientFactory,
        ILogger<DiscordRestClientProvider> logger)
    {
        if (string.IsNullOrWhiteSpace(name)) {
            return Results.Ok(Array.Empty<Gw2GuildDto>());
        }

        var trimmed = name.Trim();
        if (trimmed.Length < 3) {
            return Results.BadRequest("Search term must be at least 3 characters.");
        }

        try
        {
            var client = httpClientFactory.CreateClient("gw2-api");
            var url = $"https://api.guildwars2.com/v2/guild/search?name={Uri.EscapeDataString(trimmed)}";
            var response = await client.GetAsync(url);
            if (!response.IsSuccessStatusCode) {
                return Results.Ok(Array.Empty<Gw2GuildDto>());
            }

            var json = await response.Content.ReadAsStringAsync();
            var ids = JsonConvert.DeserializeObject<string[]>(json) ?? [];
            if (ids.Length == 0) {
                return Results.Ok(Array.Empty<Gw2GuildDto>());
            }

            var resolved = await ResolveGw2GuildNamesAsync(ids, cache, httpClientFactory, logger);
            return Results.Ok(resolved);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "GW2 guild search failed for {Name}", trimmed);
            return Results.Ok(Array.Empty<Gw2GuildDto>());
        }
    }

    internal record Gw2GuildLookup(string Name, string? Tag);

    internal enum Gw2FetchOutcome { Found, NotFound, Transient }

    internal record Gw2FetchResult(Gw2FetchOutcome Outcome, Gw2GuildLookup? Lookup);

    /// GW2 guild ids are GUIDs in 8-4-4-4-12 hex form. Pre-validating avoids a network
    /// round-trip and prevents the negative cache from filling up with junk inputs.
    internal static bool IsValidGw2GuildId(string? id) =>
        !string.IsNullOrWhiteSpace(id) && Guid.TryParseExact(id.Trim(), "D", out _);

    internal static async Task<IReadOnlyList<Gw2GuildDto>> ResolveGw2GuildNamesAsync(
        IEnumerable<string> ids,
        IMemoryCache cache,
        IHttpClientFactory httpClientFactory,
        ILogger logger)
    {
        var lookups = ids.Distinct().Select(async id =>
        {
            if (!IsValidGw2GuildId(id)) {
                return null;
            }
            var info = await GetGw2GuildLookupCachedAsync(id, cache, () => FetchGw2GuildAsync(id, httpClientFactory, logger));
            return info is null ? null : new Gw2GuildDto(id, info.Name, info.Tag);
        });
        var results = await Task.WhenAll(lookups);
        return results.Where(r => r is not null).Select(r => r!).ToList();
    }

    internal static async Task<Gw2GuildLookup?> GetGw2GuildLookupCachedAsync(
        string id,
        IMemoryCache cache,
        Func<Task<Gw2FetchResult>> fetcher)
    {
        var key = $"gw2-guild-name:{id}";
        if (cache.TryGetValue<Gw2GuildLookup?>(key, out var cached)) {
            return cached;
        }

        var result = await fetcher();
        switch (result.Outcome)
        {
            case Gw2FetchOutcome.Found:
                cache.Set(key, result.Lookup, Gw2GuildNameCacheTtl);
                return result.Lookup;
            case Gw2FetchOutcome.NotFound:
                cache.Set(key, (Gw2GuildLookup?)null, Gw2GuildNameCacheTtl);
                return null;
            default:
                // Transient: do not cache so the next attempt can succeed.
                return null;
        }
    }

    private static async Task<Gw2FetchResult> FetchGw2GuildAsync(
        string id,
        IHttpClientFactory httpClientFactory,
        ILogger logger)
    {
        try
        {
            var client = httpClientFactory.CreateClient("gw2-api");
            var response = await client.GetAsync($"https://api.guildwars2.com/v2/guild/{id}");
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound) {
                return new Gw2FetchResult(Gw2FetchOutcome.NotFound, null);
            }
            if (!response.IsSuccessStatusCode) {
                return new Gw2FetchResult(Gw2FetchOutcome.Transient, null);
            }

            var json = await response.Content.ReadAsStringAsync();
            var data = JsonConvert.DeserializeObject<GuildWars2GuildDataModel>(json);
            if (string.IsNullOrEmpty(data?.Name)) {
                return new Gw2FetchResult(Gw2FetchOutcome.NotFound, null);
            }
            return new Gw2FetchResult(Gw2FetchOutcome.Found, new Gw2GuildLookup(data.Name, data.Tag));
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to fetch GW2 guild name for {GuildId}", id);
            return new Gw2FetchResult(Gw2FetchOutcome.Transient, null);
        }
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
        if (string.IsNullOrWhiteSpace(value)) {
            return null;
        }
        if (!ulong.TryParse(value, out var parsed)) {
            return $"{fieldName} is not a valid id.";
        }
        if (!validIds.Contains(parsed)) {
            return $"{fieldName} does not belong to this guild.";
        }
        return null;
    }

    internal static string? ValidateQuoteText(string? quote)
    {
        if (string.IsNullOrWhiteSpace(quote)) {
            return "Quote cannot be empty.";
        }
        if (quote.Length > MaxQuoteLength) {
            return $"Quote must be {MaxQuoteLength} characters or fewer.";
        }
        return null;
    }

    private static async Task<IResult> ListQuotes(
        string guildId,
        ClaimsPrincipal user,
        IUserGuildsService userGuilds,
        IDbContextFactory<DatabaseContext> dbContextFactory)
    {
        if (!ulong.TryParse(guildId, out var guildIdUlong)) {
            return Results.BadRequest("Invalid guild id.");
        }
        if (!await userGuilds.HasAdministratorAsync(user, guildIdUlong)) {
            return Results.Forbid();
        }

        var guildIdLong = (long)guildIdUlong;
        await using var ctx = await dbContextFactory.CreateDbContextAsync();
        var quotes = await ctx.GuildQuote
            .Where(q => q.GuildId == guildIdLong)
            .OrderBy(q => q.GuildQuoteId)
            .Select(q => new QuoteDto(q.GuildQuoteId, q.Quote))
            .ToListAsync();
        return Results.Ok(quotes);
    }

    private static async Task<IResult> CreateQuote(
        string guildId,
        QuoteWriteDto body,
        ClaimsPrincipal user,
        IUserGuildsService userGuilds,
        IDbContextFactory<DatabaseContext> dbContextFactory)
    {
        if (!ulong.TryParse(guildId, out var guildIdUlong)) {
            return Results.BadRequest("Invalid guild id.");
        }
        if (!await userGuilds.HasAdministratorAsync(user, guildIdUlong)) {
            return Results.Forbid();
        }

        var trimmed = body.Quote?.Trim() ?? string.Empty;
        var error = ValidateQuoteText(trimmed);
        if (error is not null) {
            return Results.BadRequest(error);
        }

        var guildIdLong = (long)guildIdUlong;
        await using var ctx = await dbContextFactory.CreateDbContextAsync();
        var quote = new GuildQuote { GuildId = guildIdLong, Quote = trimmed };
        ctx.GuildQuote.Add(quote);
        await ctx.SaveChangesAsync();
        return Results.Ok(new QuoteDto(quote.GuildQuoteId, quote.Quote));
    }

    private static async Task<IResult> UpdateQuote(
        string guildId,
        long quoteId,
        QuoteWriteDto body,
        ClaimsPrincipal user,
        IUserGuildsService userGuilds,
        IDbContextFactory<DatabaseContext> dbContextFactory)
    {
        if (!ulong.TryParse(guildId, out var guildIdUlong)) {
            return Results.BadRequest("Invalid guild id.");
        }
        if (!await userGuilds.HasAdministratorAsync(user, guildIdUlong)) {
            return Results.Forbid();
        }

        var trimmed = body.Quote?.Trim() ?? string.Empty;
        var error = ValidateQuoteText(trimmed);
        if (error is not null) {
            return Results.BadRequest(error);
        }

        var guildIdLong = (long)guildIdUlong;
        await using var ctx = await dbContextFactory.CreateDbContextAsync();
        var quote = await ctx.GuildQuote.AsTracking()
            .FirstOrDefaultAsync(q => q.GuildQuoteId == quoteId && q.GuildId == guildIdLong);
        if (quote is null) {
            return Results.NotFound();
        }

        quote.Quote = trimmed;
        await ctx.SaveChangesAsync();
        return Results.Ok(new QuoteDto(quote.GuildQuoteId, quote.Quote));
    }

    private static async Task<IResult> DeleteQuote(
        string guildId,
        long quoteId,
        ClaimsPrincipal user,
        IUserGuildsService userGuilds,
        IDbContextFactory<DatabaseContext> dbContextFactory)
    {
        if (!ulong.TryParse(guildId, out var guildIdUlong)) {
            return Results.BadRequest("Invalid guild id.");
        }
        if (!await userGuilds.HasAdministratorAsync(user, guildIdUlong)) {
            return Results.Forbid();
        }

        var guildIdLong = (long)guildIdUlong;
        await using var ctx = await dbContextFactory.CreateDbContextAsync();
        var quote = await ctx.GuildQuote.AsTracking()
            .FirstOrDefaultAsync(q => q.GuildQuoteId == quoteId && q.GuildId == guildIdLong);
        if (quote is null) {
            return Results.NotFound();
        }

        ctx.GuildQuote.Remove(quote);
        await ctx.SaveChangesAsync();
        return Results.NoContent();
    }

    private static bool TryGetDiscordId(ClaimsPrincipal user, out ulong discordId)
    {
        discordId = 0;
        var raw = user.FindFirst("discord_id")?.Value;
        return ulong.TryParse(raw, out discordId);
    }

}
