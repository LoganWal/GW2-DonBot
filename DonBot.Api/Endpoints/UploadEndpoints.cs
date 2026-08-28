using System.Net;
using System.Security.Claims;
using System.Text;
using DonBot.Api.Services;
using DonBot.Core.Models;
using DonBot.Core.Models.Entities;
using DonBot.Core.Services.GuildWars2;
using DonBot.Models.Apis.GuildWars2Api;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using tusdotnet;
using tusdotnet.Interfaces;
using tusdotnet.Models;
using tusdotnet.Models.Configuration;
using tusdotnet.Stores;

namespace DonBot.Api.Endpoints;

public static class UploadEndpoints
{
    private static readonly System.Text.Json.JsonSerializerOptions SseJsonOptions =
        new(System.Text.Json.JsonSerializerDefaults.Web);
    private const string Gw2ApiKeyHeader = "X-GW2-API-Key";
    private const string TusUploadIdentityItemKey = "donbot:tus-upload-identity";
    private const string TusDiscordDeliveryItemKey = "donbot:tus-discord-delivery";

    public static void MapUploadEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/upload");
        group.MapPost("/urls", SubmitUrls).RequireAuthorization();
        group.MapGet("/history", GetHistory).RequireAuthorization();
        group.MapGet("/stream/{id:long}", StreamProgress).AllowAnonymous();
        group.MapPost("/wingman/{id:long}", SubmitOneToWingman).RequireAuthorization();
        group.MapPost("/wingman/bulk", SubmitBulkToWingman).RequireAuthorization();
        group.MapPost("/gw2/guilds", ListGw2UploadGuilds).AllowAnonymous();
        group.MapPost("/gw2/url", SubmitGw2Url).AllowAnonymous();
        group.MapTus("/tus", _ => BuildTusConfigurationAsync(app)).AllowAnonymous();
    }

    private static Task<DefaultTusConfiguration> BuildTusConfigurationAsync(WebApplication app)
    {
        var storagePath = app.Configuration["Upload:StoragePath"] ?? "/tmp/donbot/uploads";
        var maxUploadBytes = app.Configuration.GetValue<long>("Upload:MaxRequestBytes", 1_073_741_824);
        var tusTempPath = Path.Combine(storagePath, "tus-temp");
        Directory.CreateDirectory(tusTempPath);

        return Task.FromResult(new DefaultTusConfiguration
        {
            Store = new TusDiskStore(tusTempPath),
            MaxAllowedUploadSizeInBytesLong = maxUploadBytes,
            Events = new Events
            {
                OnAuthorizeAsync = AuthorizeTusRequestAsync,
                OnBeforeCreateAsync = async ctx =>
                {
                    if (!TryGetMetadataString(ctx.Metadata, "filename", out var filename) ||
                        !filename.EndsWith(".zevtc", StringComparison.OrdinalIgnoreCase))
                    {
                        ctx.FailRequest(HttpStatusCode.BadRequest, "Only .zevtc files are allowed.");
                        return;
                    }

                    var guildResult = await ResolveTusGuildIdAsync(ctx.HttpContext, ctx.Metadata, ctx.CancellationToken);

                    if (guildResult.FailureStatus is { } status)
                    {
                        ctx.FailRequest(status, guildResult.FailureMessage ?? "Invalid guild id.");
                        return;
                    }

                    var deliveryResult = await ResolveTusDiscordDeliveryAsync(
                        ctx.HttpContext,
                        ctx.Metadata,
                        guildResult.GuildId,
                        ctx.CancellationToken);
                    if (deliveryResult.FailureStatus is { } deliveryStatus)
                    {
                        ctx.FailRequest(deliveryStatus, deliveryResult.FailureMessage ?? "Invalid Discord delivery request.");
                    }
                },
                OnCreateCompleteAsync = async ctx =>
                {
                    var identityResult = await ResolveTusUploadIdentityAsync(ctx.HttpContext, ctx.CancellationToken);
                    if (identityResult.Identity is not { } identity)
                    {
                        return;
                    }

                    var filename = TryGetMetadataString(ctx.Metadata, "filename", out var metadataFileName)
                        ? metadataFileName
                        : "upload.zevtc";
                    var safeName = Path.GetFileName(filename);
                    var wingman = TryGetMetadataString(ctx.Metadata, "wingman", out var wingmanRaw) &&
                        string.Equals(wingmanRaw, "true", StringComparison.OrdinalIgnoreCase);

                    var guildResult = await ResolveTusGuildIdAsync(ctx.HttpContext, ctx.Metadata, ctx.CancellationToken);

                    if (guildResult.FailureStatus is not null)
                    {
                        return;
                    }

                    var deliveryResult = await ResolveTusDiscordDeliveryAsync(
                        ctx.HttpContext,
                        ctx.Metadata,
                        guildResult.GuildId,
                        ctx.CancellationToken);
                    if (deliveryResult.FailureStatus is not null)
                    {
                        return;
                    }

                    var dbFactory = ctx.HttpContext.RequestServices
                        .GetRequiredService<IDbContextFactory<DatabaseContext>>();
                    await using var db = await dbFactory.CreateDbContextAsync(ctx.CancellationToken);

                    var upload = new LogUpload
                    {
                        DiscordId = identity.DiscordId,
                        FileName = safeName,
                        SourceType = "file",
                        Status = "receiving",
                        SubmitToWingman = wingman,
                        GuildId = guildResult.GuildId,
                        TusFileId = ctx.FileId,
                        DiscordDeliveryMode = deliveryResult.Mode,
                        DiscordDeliveryChannelId = deliveryResult.ChannelId,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    };
                    db.LogUpload.Add(upload);
                    await db.SaveChangesAsync(ctx.CancellationToken);

                    ctx.HttpContext.Response.Headers["X-Log-Upload-Id"] = upload.LogUploadId.ToString();
                    if (deliveryResult.Mode is not null)
                    {
                        ctx.HttpContext.Response.Headers["X-DonBot-Discord-Delivery"] = "accepted";
                    }
                },
                OnFileCompleteAsync = async ctx =>
                {
                    var uploadStoragePath = ctx.HttpContext.RequestServices
                        .GetRequiredService<IConfiguration>()["Upload:StoragePath"] ?? "/tmp/donbot/uploads";

                    var dbFactory = ctx.HttpContext.RequestServices
                        .GetRequiredService<IDbContextFactory<DatabaseContext>>();
                    await using var db = await dbFactory.CreateDbContextAsync(ctx.CancellationToken);
                    var upload = await db.LogUpload.FirstOrDefaultAsync(
                        u => u.TusFileId == ctx.FileId, ctx.CancellationToken);
                    if (upload is null)
                    {
                        return;
                    }

                    var logUploadId = upload.LogUploadId;
                    var uploadDir = Path.Combine(uploadStoragePath, logUploadId.ToString());
                    Directory.CreateDirectory(uploadDir);

                    var file = await ctx.GetFileAsync();
                    await using (var content = await file.GetContentAsync(ctx.CancellationToken))
                    await using (var dest = File.Create(Path.Combine(uploadDir, upload.FileName)))
                    {
                        await content.CopyToAsync(dest, ctx.CancellationToken);
                    }

                    if (ctx.Store is ITusTerminationStore terminationStore)
                    {
                        await terminationStore.DeleteFileAsync(ctx.FileId, ctx.CancellationToken);
                    }

                    upload.Status = "stored";
                    upload.UpdatedAt = DateTime.UtcNow;
                    db.LogUpload.Update(upload);
                    await db.SaveChangesAsync(ctx.CancellationToken);

                    ctx.HttpContext.RequestServices.GetRequiredService<LogUploadPipelineService>()
                        .Enqueue(logUploadId);
                }
            }
        });
    }

    internal static async Task<bool> IsTusUploadOwnerAsync(
        IDbContextFactory<DatabaseContext> dbFactory,
        string tusFileId,
        long discordId,
        CancellationToken ct)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await db.LogUpload.AsNoTracking().AnyAsync(
            u => u.TusFileId == tusFileId && u.DiscordId == discordId,
            ct);
    }

    private static bool TryGetDiscordId(ClaimsPrincipal user, out long discordId)
    {
        if (!(user.Identity?.IsAuthenticated ?? false))
        {
            discordId = 0;
            return false;
        }

        return long.TryParse(user.FindFirst("discord_id")?.Value, out discordId);
    }

    internal static async Task<TusGuildResolution> ResolveTusGuildIdAsync(
        IReadOnlyDictionary<string, Metadata> metadata,
        ClaimsPrincipal user,
        IUserGuildsService guildService,
        CancellationToken ct)
    {
        if (!TryGetMetadataString(metadata, "guildid", out var guildIdRaw) &&
            !TryGetMetadataString(metadata, "guildId", out guildIdRaw))
        {
            return new TusGuildResolution(0);
        }

        if (!long.TryParse(guildIdRaw, out var guildId) || guildId <= 0)
        {
            return TusGuildResolution.Failed(HttpStatusCode.BadRequest, "Invalid guild id.");
        }

        var userGuildList = await guildService.GetForPrincipalAsync(user, ct);
        if (userGuildList is null || userGuildList.All(guild => (long)guild.Id != guildId))
        {
            return TusGuildResolution.Failed(HttpStatusCode.Forbidden, "You are not a member of that guild.");
        }

        return new TusGuildResolution(guildId);
    }

    internal static TusGuildResolution ResolveTusGuildIdAsync(
        IReadOnlyDictionary<string, Metadata> metadata,
        IReadOnlySet<long> allowedGuildIds)
    {
        if (!TryGetMetadataString(metadata, "guildid", out var guildIdRaw) &&
            !TryGetMetadataString(metadata, "guildId", out guildIdRaw))
        {
            return TusGuildResolution.Failed(HttpStatusCode.BadRequest, "Guild id is required.");
        }

        if (!long.TryParse(guildIdRaw, out var guildId) || guildId <= 0)
        {
            return TusGuildResolution.Failed(HttpStatusCode.BadRequest, "Invalid guild id.");
        }

        if (!allowedGuildIds.Contains(guildId))
        {
            return TusGuildResolution.Failed(HttpStatusCode.Forbidden, "You are not allowed to upload to that guild.");
        }

        return new TusGuildResolution(guildId);
    }

    private static async Task<TusGuildResolution> ResolveTusGuildIdAsync(
        HttpContext httpContext,
        IReadOnlyDictionary<string, Metadata> metadata,
        CancellationToken ct)
    {
        var identityResult = await ResolveTusUploadIdentityAsync(httpContext, ct);
        if (identityResult.Identity is not { } identity)
        {
            return TusGuildResolution.Failed(
                identityResult.FailureStatus ?? HttpStatusCode.Unauthorized,
                identityResult.FailureMessage ?? "Unauthorized.");
        }

        if (identity.AllowedGuildIds is not null)
        {
            return ResolveTusGuildIdAsync(metadata, identity.AllowedGuildIds);
        }

        var guildService = httpContext.RequestServices.GetRequiredService<IUserGuildsService>();
        return await ResolveTusGuildIdAsync(metadata, httpContext.User, guildService, ct);
    }

    private static async Task<TusUploadIdentityResult> ResolveTusUploadIdentityAsync(
        HttpContext httpContext,
        CancellationToken ct)
    {
        if (httpContext.Items.TryGetValue(TusUploadIdentityItemKey, out var cached) &&
            cached is TusUploadIdentityResult cachedResult)
        {
            return cachedResult;
        }

        TusUploadIdentityResult result;
        if (TryGetDiscordId(httpContext.User, out var discordId))
        {
            result = TusUploadIdentityResult.Success(new TusUploadIdentity(discordId, null));
        }
        else if (TryGetGw2ApiKey(httpContext.Request, out var apiKey))
        {
            var access = await ResolveGw2UploadAccessAsync(
                apiKey,
                httpContext.RequestServices.GetRequiredService<IDbContextFactory<DatabaseContext>>(),
                httpContext.RequestServices.GetRequiredService<IHttpClientFactory>(),
                httpContext.RequestServices.GetRequiredService<IDiscordGuildMembershipService>(),
                httpContext.RequestServices.GetRequiredService<IDiscordUploadDeliveryService>(),
                includeDiscordDelivery: false,
                ct: ct);

            result = access.Access is { } identity
                ? TusUploadIdentityResult.Success(new TusUploadIdentity(
                    identity.DiscordId,
                    identity.Guilds
                        .Select(g => long.TryParse(g.GuildId, out var guildId) ? guildId : 0)
                        .Where(guildId => guildId > 0)
                        .ToHashSet()))
                : TusUploadIdentityResult.Failed(
                    access.FailureStatus ?? HttpStatusCode.Unauthorized,
                    access.FailureMessage ?? "Unauthorized.");
        }
        else
        {
            result = TusUploadIdentityResult.Failed(HttpStatusCode.Unauthorized, "Unauthorized.");
        }

        httpContext.Items[TusUploadIdentityItemKey] = result;
        return result;
    }

    private static async Task<TusDiscordDeliveryResolution> ResolveTusDiscordDeliveryAsync(
        HttpContext httpContext,
        IReadOnlyDictionary<string, Metadata> metadata,
        long guildId,
        CancellationToken ct)
    {
        if (httpContext.Items.TryGetValue(TusDiscordDeliveryItemKey, out var cached) &&
            cached is TusDiscordDeliveryResolution cachedResult)
        {
            return cachedResult;
        }

        var hasMode = TryGetMetadataString(metadata, "discorddelivery", out var mode);
        var hasChannel = TryGetMetadataString(metadata, "discordchannelid", out var channelIdRaw);
        TusDiscordDeliveryResolution result;
        if (!hasMode)
        {
            result = hasChannel
                ? TusDiscordDeliveryResolution.Failed(HttpStatusCode.BadRequest, "Invalid Discord delivery request.")
                : new TusDiscordDeliveryResolution(null, null);
        }
        else if (mode == DiscordDeliveryModes.GuildDefaults && !hasChannel)
        {
            result = new TusDiscordDeliveryResolution(DiscordDeliveryModes.GuildDefaults, null);
        }
        else if (mode == DiscordDeliveryModes.ChannelOverride &&
            hasChannel &&
            TryParseCanonicalPositiveInt64(channelIdRaw, out var channelId))
        {
            result = new TusDiscordDeliveryResolution(DiscordDeliveryModes.ChannelOverride, channelId);
        }
        else
        {
            result = TusDiscordDeliveryResolution.Failed(HttpStatusCode.BadRequest, "Invalid Discord delivery request.");
        }

        if (result.FailureStatus is null && result.Mode is not null)
        {
            var identityResult = await ResolveTusUploadIdentityAsync(httpContext, ct);
            if (identityResult.Identity is not { } identity || guildId <= 0)
            {
                result = TusDiscordDeliveryResolution.Failed(HttpStatusCode.Forbidden, "Discord delivery is not authorized.");
            }
            else
            {
                var validation = await httpContext.RequestServices
                    .GetRequiredService<IDiscordUploadDeliveryService>()
                    .ValidateAsync(identity.DiscordId, guildId, result.Mode, result.ChannelId, ct);
                if (!validation.Accepted)
                {
                    result = TusDiscordDeliveryResolution.Failed(
                        validation.ErrorCode == "discord_channel_forbidden"
                            ? HttpStatusCode.Forbidden
                            : HttpStatusCode.BadRequest,
                        "Discord delivery is not authorized.");
                }
            }
        }

        httpContext.Items[TusDiscordDeliveryItemKey] = result;
        return result;
    }

    private static bool TryGetGw2ApiKey(HttpRequest request, out string apiKey)
    {
        apiKey = string.Empty;
        if (!request.Headers.TryGetValue(Gw2ApiKeyHeader, out var values))
        {
            return false;
        }

        apiKey = values.FirstOrDefault()?.Trim() ?? string.Empty;
        return !string.IsNullOrWhiteSpace(apiKey);
    }

    private static async Task<Gw2UploadAccessResult> ResolveGw2UploadAccessAsync(
        string? apiKey,
        IDbContextFactory<DatabaseContext> dbContextFactory,
        IHttpClientFactory httpClientFactory,
        IDiscordGuildMembershipService guildMembershipService,
        IDiscordUploadDeliveryService discordDeliveryService,
        bool includeDiscordDelivery,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return Gw2UploadAccessResult.Failed(HttpStatusCode.BadRequest, "GW2 API key is required.");
        }

        var accountResult = await FetchGw2AccountAsync(apiKey.Trim(), httpClientFactory, ct);
        if (accountResult.Access is not { } accountData)
        {
            return Gw2UploadAccessResult.Failed(
                accountResult.FailureStatus ?? HttpStatusCode.BadRequest,
                accountResult.FailureMessage ?? "Invalid GW2 API key.");
        }

        var accountName = accountData.Name?.Trim() ?? string.Empty;
        if (accountData.Id == Guid.Empty || string.IsNullOrWhiteSpace(accountName))
        {
            return Gw2UploadAccessResult.Failed(HttpStatusCode.BadRequest, "Invalid GW2 API account response.");
        }

        await using var context = await dbContextFactory.CreateDbContextAsync(ct);
        var linkedAccount = await context.GuildWarsAccount
            .AsNoTracking()
            .Where(a => a.GuildWarsAccountId == accountData.Id || a.GuildWarsAccountName == accountName)
            .Select(a => new { a.DiscordId })
            .FirstOrDefaultAsync(ct);

        if (linkedAccount is null)
        {
            return Gw2UploadAccessResult.Failed(HttpStatusCode.Forbidden, "GW2 account is not linked to DonBot.");
        }

        var guilds = await ListUploadGuildsForDiscordUserAsync(
            context,
            linkedAccount.DiscordId,
            guildMembershipService,
            discordDeliveryService,
            includeDiscordDelivery,
            ct);

        return Gw2UploadAccessResult.Success(new Gw2UploadAccess(linkedAccount.DiscordId, accountName, guilds));
    }

    private static async Task<Gw2AccountResult> FetchGw2AccountAsync(
        string apiKey,
        IHttpClientFactory httpClientFactory,
        CancellationToken ct)
    {
        HttpResponseMessage response;
        try
        {
            var client = httpClientFactory.CreateClient();
            response = await client.GetAsync(
                $"https://api.guildwars2.com/v2/account/?access_token={Uri.EscapeDataString(apiKey)}",
                ct);
        }
        catch (HttpRequestException)
        {
            return Gw2AccountResult.Failed(HttpStatusCode.BadGateway, "Could not reach the GW2 API.");
        }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested)
        {
            return Gw2AccountResult.Failed(HttpStatusCode.BadGateway, "GW2 API request timed out.");
        }

        if (!response.IsSuccessStatusCode)
        {
            return Gw2AccountResult.Failed(HttpStatusCode.BadRequest, "Invalid GW2 API key.");
        }

        var json = await response.Content.ReadAsStringAsync(ct);
        try
        {
            return Gw2AccountResult.Success(
                JsonConvert.DeserializeObject<GuildWars2AccountDataModel>(json) ?? new GuildWars2AccountDataModel());
        }
        catch (JsonException)
        {
            return Gw2AccountResult.Failed(HttpStatusCode.BadGateway, "GW2 API returned invalid account data.");
        }
    }

    private static async Task<IReadOnlyList<GuildSummaryDto>> ListUploadGuildsForDiscordUserAsync(
        DatabaseContext context,
        long discordId,
        IDiscordGuildMembershipService guildMembershipService,
        IDiscordUploadDeliveryService discordDeliveryService,
        bool includeDiscordDelivery,
        CancellationToken ct)
    {
        var configuredGuilds = await context.Guild
            .AsNoTracking()
            .Where(guild => guild.GuildId > 0)
            .OrderBy(guild => guild.GuildName ?? guild.GuildId.ToString())
            .ToListAsync(ct);

        var configuredGuildIds = configuredGuilds
            .Select(g => g.GuildId)
            .ToArray();
        var memberGuildIds = await guildMembershipService.GetMemberGuildIdsAsync(discordId, configuredGuildIds, ct);

        var authorizedGuilds = configuredGuilds
            .Where(g => memberGuildIds.Contains(g.GuildId))
            .OrderBy(g => g.GuildName ?? g.GuildId.ToString())
            .Take(256)
            .ToList();

        var result = new List<GuildSummaryDto>(authorizedGuilds.Count);
        // A lower practical cap keeps worst-case UTF-8 names within the 256 KiB response budget.
        var remainingChannels = 384;
        foreach (var guild in authorizedGuilds)
        {
            var capabilities = includeDiscordDelivery
                ? await discordDeliveryService.GetCapabilitiesAsync(guild, discordId, ct)
                : new DiscordDeliveryCapabilities(false, false, false, [], []);
            var channels = capabilities.Channels
                .Take(remainingChannels)
                .Select(channel => new DiscordChannelDto(
                    channel.ChannelId.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    channel.ChannelName))
                .ToList();
            remainingChannels -= channels.Count;

            result.Add(new GuildSummaryDto(
                guild.GuildId.ToString(System.Globalization.CultureInfo.InvariantCulture),
                NormalizeContractName(guild.GuildName ?? guild.GuildId.ToString()),
                new DiscordDeliveryCapabilitiesDto(
                    capabilities.Enabled,
                    capabilities.DefaultsAvailable,
                    capabilities.ChannelOverrideAllowed,
                    capabilities.EnabledMessageKinds,
                    channels)));
        }

        return result;
    }

    private static string NormalizeContractName(string value)
    {
        var sanitized = new string(value.Where(character => character >= ' ' && character != '\u007f').ToArray());
        while (Encoding.UTF8.GetByteCount(sanitized) > 256)
        {
            sanitized = sanitized[..^1];
        }

        return sanitized;
    }

    private static bool TryGetMetadataString(
        IReadOnlyDictionary<string, Metadata> metadata,
        string key,
        out string value)
    {
        if (metadata.TryGetValue(key, out var metadataValue))
        {
            value = metadataValue.GetString(Encoding.UTF8);
            return true;
        }

        value = string.Empty;
        return false;
    }

    internal readonly record struct TusGuildResolution(
        long GuildId,
        HttpStatusCode? FailureStatus = null,
        string? FailureMessage = null)
    {
        public static TusGuildResolution Failed(HttpStatusCode status, string message) => new(0, status, message);
    }

    private readonly record struct TusDiscordDeliveryResolution(
        string? Mode,
        long? ChannelId,
        HttpStatusCode? FailureStatus = null,
        string? FailureMessage = null)
    {
        public static TusDiscordDeliveryResolution Failed(HttpStatusCode status, string message) =>
            new(null, null, status, message);
    }

    private static async Task<IResult> ListGw2UploadGuilds(
        Gw2UploadGuildsRequest request,
        IDbContextFactory<DatabaseContext> dbContextFactory,
        IHttpClientFactory httpClientFactory,
        IDiscordGuildMembershipService guildMembershipService,
        IDiscordUploadDeliveryService discordDeliveryService,
        CancellationToken ct)
    {
        var result = await ResolveGw2UploadAccessAsync(
            request.ApiKey,
            dbContextFactory,
            httpClientFactory,
            guildMembershipService,
            discordDeliveryService,
            includeDiscordDelivery: true,
            ct: ct);
        if (result.Access is not { } access)
        {
            return UploadAuthFailure(result.FailureStatus ?? HttpStatusCode.BadRequest, result.FailureMessage);
        }

        return Results.Ok(new Gw2UploadGuildsResponse(access.AccountName, access.Guilds));
    }

    private static IResult UploadAuthFailure(HttpStatusCode status, string? message)
    {
        return status switch
        {
            HttpStatusCode.BadRequest => Results.BadRequest(message ?? "Bad request."),
            HttpStatusCode.Unauthorized => Results.Unauthorized(),
            HttpStatusCode.Forbidden => Results.Json(
                message ?? "Forbidden.",
                statusCode: StatusCodes.Status403Forbidden),
            _ => Results.Json(
                message ?? "Upload authorization failed.",
                statusCode: (int)status)
        };
    }

    private static async Task<IResult> SubmitGw2Url(
        HttpContext httpContext,
        IDbContextFactory<DatabaseContext> dbContextFactory,
        IHttpClientFactory httpClientFactory,
        IDiscordGuildMembershipService guildMembershipService,
        IDiscordUploadDeliveryService discordDeliveryService,
        LogUploadPipelineService pipeline,
        CancellationToken ct)
    {
        if (!TryGetGw2ApiKey(httpContext.Request, out var apiKey))
        {
            return Gw2UrlError(StatusCodes.Status400BadRequest, "gw2_api_key_required");
        }

        Gw2UploadAccessResult accessResult;
        try
        {
            accessResult = await ResolveGw2UploadAccessAsync(
                apiKey,
                dbContextFactory,
                httpClientFactory,
                guildMembershipService,
                discordDeliveryService,
                includeDiscordDelivery: false,
                ct: ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            return Gw2UrlError(StatusCodes.Status500InternalServerError, "server_error");
        }

        if (accessResult.Access is not { } access)
        {
            var statusCode = accessResult.FailureStatus switch
            {
                HttpStatusCode.Forbidden => StatusCodes.Status403Forbidden,
                HttpStatusCode.Unauthorized => StatusCodes.Status401Unauthorized,
                HttpStatusCode.BadRequest => StatusCodes.Status400BadRequest,
                HttpStatusCode.BadGateway => StatusCodes.Status502BadGateway,
                _ => StatusCodes.Status500InternalServerError
            };
            var errorCode = statusCode switch
            {
                StatusCodes.Status403Forbidden => "gw2_account_not_linked",
                StatusCodes.Status400BadRequest or StatusCodes.Status401Unauthorized => "invalid_gw2_api_key",
                _ => "upload_authorization_failed"
            };
            return Gw2UrlError(statusCode, errorCode);
        }

        SubmitGw2UrlRequest? request;
        try
        {
            request = await httpContext.Request.ReadFromJsonAsync<SubmitGw2UrlRequest>(cancellationToken: ct);
        }
        catch (Exception ex) when (ex is
            System.Text.Json.JsonException or
            BadHttpRequestException or
            NotSupportedException or
            InvalidOperationException)
        {
            return Gw2UrlError(StatusCodes.Status400BadRequest, "invalid_request");
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            return Gw2UrlError(StatusCodes.Status500InternalServerError, "server_error");
        }

        if (request is null ||
            request.AdditionalProperties?.Keys.Any(
                property => string.Equals(property, "wingman", StringComparison.OrdinalIgnoreCase)) == true ||
            !TryParseCanonicalDpsReportPermalink(request.Url, out var parsedUrl) ||
            !TryParseCanonicalPositiveInt64(request.GuildId, out var guildId))
        {
            return Gw2UrlError(StatusCodes.Status400BadRequest, "invalid_request");
        }

        if (!access.Guilds.Any(guild => string.Equals(guild.GuildId, request.GuildId, StringComparison.Ordinal)))
        {
            return Gw2UrlError(StatusCodes.Status403Forbidden, "guild_forbidden");
        }

        if (!TryNormalizeDiscordDelivery(request.DiscordDelivery, out var deliveryMode, out var deliveryChannelId))
        {
            return Gw2UrlError(StatusCodes.Status400BadRequest, "invalid_discord_delivery");
        }

        if (deliveryMode is not null)
        {
            var deliveryValidation = await discordDeliveryService.ValidateAsync(
                access.DiscordId,
                guildId,
                deliveryMode,
                deliveryChannelId,
                ct);
            if (!deliveryValidation.Accepted)
            {
                return Gw2UrlError(
                    deliveryValidation.ErrorCode == "discord_channel_forbidden"
                        ? StatusCodes.Status403Forbidden
                        : StatusCodes.Status400BadRequest,
                    deliveryValidation.ErrorCode ?? "invalid_discord_delivery");
            }
        }

        try
        {
            await using var context = await dbContextFactory.CreateDbContextAsync(ct);
            var existing = await FindGw2UrlImportAsync(
                context,
                access.DiscordId,
                guildId,
                parsedUrl.CanonicalUrl,
                ct);
            if (existing is not null)
            {
                return await ExistingGw2UrlImportAsync(
                    existing,
                    deliveryMode,
                    deliveryChannelId,
                    discordDeliveryService,
                    ct);
            }

            var now = DateTime.UtcNow;
            var upload = new LogUpload
            {
                DiscordId = access.DiscordId,
                GuildId = guildId,
                FileName = parsedUrl.Permalink,
                SourceType = "url",
                Status = "pending",
                DpsReportUrl = parsedUrl.CanonicalUrl,
                SubmitToWingman = false,
                DiscordDeliveryMode = deliveryMode,
                DiscordDeliveryChannelId = deliveryChannelId,
                CreatedAt = now,
                UpdatedAt = now
            };
            context.LogUpload.Add(upload);

            try
            {
                await context.SaveChangesAsync(ct);
            }
            catch (DbUpdateException)
            {
                context.ChangeTracker.Clear();
                existing = await FindGw2UrlImportAsync(
                    context,
                    access.DiscordId,
                    guildId,
                    parsedUrl.CanonicalUrl,
                    ct);
                if (existing is null)
                {
                    return Gw2UrlError(StatusCodes.Status500InternalServerError, "server_error");
                }

                return await ExistingGw2UrlImportAsync(
                    existing,
                    deliveryMode,
                    deliveryChannelId,
                    discordDeliveryService,
                    ct);
            }

            pipeline.Enqueue(upload.LogUploadId);
            return Results.Accepted(value: Gw2UrlResponse.From(upload, duplicate: false, discordDelivery: null));
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            return Gw2UrlError(StatusCodes.Status500InternalServerError, "server_error");
        }
    }

    private static bool TryParseCanonicalDpsReportPermalink(string? url, out ParsedReportUrl parsedUrl)
    {
        parsedUrl = null!;
        if (string.IsNullOrEmpty(url) ||
            Encoding.UTF8.GetByteCount(url) > 2048 ||
            url.Any(character => character == '\\' || char.IsControl(character)) ||
            !Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
            !string.IsNullOrEmpty(uri.UserInfo) ||
            !string.IsNullOrEmpty(uri.Fragment) ||
            !string.IsNullOrEmpty(uri.Query) ||
            !uri.IsDefaultPort ||
            !ReportUrlHelper.TryParseReportUrl(url, out parsedUrl, requireHttps: true) ||
            parsedUrl.Kind != ReportUrlKind.DpsReport ||
            !string.Equals(parsedUrl.Host, "dps.report", StringComparison.Ordinal) ||
            !string.Equals(url, parsedUrl.CanonicalUrl, StringComparison.Ordinal))
        {
            parsedUrl = null!;
            return false;
        }

        return true;
    }

    private static bool TryParseCanonicalPositiveInt64(string? value, out long result)
    {
        result = 0;
        return !string.IsNullOrEmpty(value) &&
            long.TryParse(value, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out result) &&
            result > 0 &&
            string.Equals(value, result.ToString(System.Globalization.CultureInfo.InvariantCulture), StringComparison.Ordinal);
    }

    private static bool TryNormalizeDiscordDelivery(
        SubmitDiscordDeliveryRequest? request,
        out string? mode,
        out long? channelId)
    {
        mode = null;
        channelId = null;
        if (request is null)
        {
            return true;
        }

        if (request.AdditionalProperties?.Count > 0)
        {
            return false;
        }

        if (string.Equals(request.Mode, DiscordDeliveryModes.GuildDefaults, StringComparison.Ordinal))
        {
            if (request.ChannelId.HasValue)
            {
                return false;
            }

            mode = DiscordDeliveryModes.GuildDefaults;
            return true;
        }

        if (!string.Equals(request.Mode, DiscordDeliveryModes.ChannelOverride, StringComparison.Ordinal) ||
            request.ChannelId is not { ValueKind: System.Text.Json.JsonValueKind.String } channelElement ||
            !TryParseCanonicalPositiveInt64(channelElement.GetString(), out var parsedChannelId))
        {
            return false;
        }

        mode = DiscordDeliveryModes.ChannelOverride;
        channelId = parsedChannelId;
        return true;
    }

    private static Task<LogUpload?> FindGw2UrlImportAsync(
        DatabaseContext context,
        long discordId,
        long guildId,
        string canonicalUrl,
        CancellationToken ct) =>
        context.LogUpload
            .AsNoTracking()
            .FirstOrDefaultAsync(
                upload => upload.DiscordId == discordId &&
                    upload.GuildId == guildId &&
                    upload.DpsReportUrl == canonicalUrl &&
                    upload.SourceType == "url",
                ct);

    private static async Task<IResult> ExistingGw2UrlImportAsync(
        LogUpload upload,
        string? requestedDeliveryMode,
        long? requestedDeliveryChannelId,
        IDiscordUploadDeliveryService discordDeliveryService,
        CancellationToken ct)
    {
        if (string.Equals(upload.Status, "failed", StringComparison.Ordinal))
        {
            return Gw2UrlError(StatusCodes.Status409Conflict, "import_failed");
        }

        if (!string.Equals(upload.DiscordDeliveryMode, requestedDeliveryMode, StringComparison.Ordinal) ||
            upload.DiscordDeliveryChannelId != requestedDeliveryChannelId)
        {
            return Gw2UrlError(StatusCodes.Status409Conflict, "discord_delivery_conflict");
        }

        var deliveryResult = upload.Status == "complete" && upload.DiscordDeliveryMode is not null
            ? await discordDeliveryService.GetResultAsync(upload.LogUploadId, ct)
            : null;
        return Results.Ok(Gw2UrlResponse.From(upload, duplicate: true, deliveryResult));
    }

    private static IResult Gw2UrlError(int statusCode, string errorCode) =>
        Results.Json(new Gw2UrlErrorResponse(errorCode), statusCode: statusCode);

    private static async Task<IResult> SubmitUrls(
        SubmitUrlsRequest request,
        ClaimsPrincipal user,
        IDbContextFactory<DatabaseContext> dbContextFactory,
        LogUploadPipelineService pipeline)
    {
        var discordIdStr = user.FindFirst("discord_id")?.Value;
        if (!long.TryParse(discordIdStr, out var discordId))
        {
            return Results.Unauthorized();
        }

        var urls = request.Urls ?? [];
        if (urls.Length == 0)
        {
            return Results.BadRequest("No URLs provided.");
        }

        var validUrls = urls
            .Select(u => u.Trim())
            .Select(u => ReportUrlHelper.TryParseReportUrl(u, out var parsed) ? parsed : null)
            .OfType<ParsedReportUrl>()
            .DistinctBy(u => u.CanonicalUrl, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (validUrls.Count == 0)
        {
            return Results.BadRequest("No valid dps.report or wvw.report URLs provided.");
        }

        await using var ctx = await dbContextFactory.CreateDbContextAsync();
        var created = new List<object>();

        foreach (var parsedUrl in validUrls)
        {
            var url = parsedUrl.CanonicalUrl;
            var displayName = parsedUrl.Permalink;

            var upload = new LogUpload
            {
                DiscordId = discordId,
                FileName = displayName,
                SourceType = "url",
                Status = "pending",
                DpsReportUrl = url,
                SubmitToWingman = request.Wingman,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            ctx.LogUpload.Add(upload);
            await ctx.SaveChangesAsync();

            pipeline.Enqueue(upload.LogUploadId);
            created.Add(new { upload.LogUploadId, upload.FileName, sourceType = "url" });
        }

        return Results.Ok(created);
    }

    private static async Task StreamProgress(
        long id,
        ILogUploadProgressService progress,
        IDbContextFactory<DatabaseContext> dbContextFactory,
        IDiscordUploadDeliveryService discordDeliveryService,
        HttpContext ctx,
        CancellationToken ct)
    {
        var identityResult = await ResolveTusUploadIdentityAsync(ctx, ct);
        if (identityResult.Identity is not { } identity)
        {
            ctx.Response.StatusCode = (int)(identityResult.FailureStatus ?? HttpStatusCode.Unauthorized);
            return;
        }

        await using (var context = await dbContextFactory.CreateDbContextAsync(ct))
        {
            var upload = await context.LogUpload.AsNoTracking()
                .FirstOrDefaultAsync(
                    item => item.LogUploadId == id && item.DiscordId == identity.DiscordId,
                    ct);
            if (upload is null)
            {
                ctx.Response.StatusCode = StatusCodes.Status404NotFound;
                return;
            }

            SseWriter.Prepare(ctx.Response);
            if (upload?.Status == "complete")
            {
                var deliveryResult = await discordDeliveryService.GetResultAsync(id, ct);
                var payload = System.Text.Json.JsonSerializer.Serialize(
                    new
                    {
                        stage = "complete",
                        message = "Done.",
                        dpsReportUrl = upload.DpsReportUrl,
                        fightLogId = upload.FightLogId,
                        discordDelivery = deliveryResult
                    },
                    SseJsonOptions);
                await SseWriter.WriteDataAsync(ctx.Response, payload, ct);
                return;
            }

            if (upload?.Status == "failed")
            {
                var payload = System.Text.Json.JsonSerializer.Serialize(
                    new
                    {
                        stage = "failed",
                        message = upload.ErrorMessage ?? "Upload processing failed.",
                        dpsReportUrl = upload.DpsReportUrl,
                        fightLogId = upload.FightLogId,
                        discordDelivery = upload.DiscordDeliveryMode is null
                            ? DiscordDeliveryResult.NotRequested
                            : await discordDeliveryService.GetResultAsync(id, ct)
                    },
                    SseJsonOptions);
                await SseWriter.WriteDataAsync(ctx.Response, payload, ct);
                return;
            }
        }

        await foreach (var msg in progress.Subscribe(id, ct))
        {
            await SseWriter.WriteDataAsync(ctx.Response, msg, ct);
        }
    }

    private static async Task<IResult> SubmitOneToWingman(
        long id,
        ClaimsPrincipal user,
        IDbContextFactory<DatabaseContext> dbContextFactory,
        LogUploadPipelineService pipeline)
    {
        var discordIdStr = user.FindFirst("discord_id")?.Value;
        if (!long.TryParse(discordIdStr, out var discordId))
        {
            return Results.Unauthorized();
        }

        await using var ctx = await dbContextFactory.CreateDbContextAsync();
        var upload = await ctx.LogUpload.FirstOrDefaultAsync(u => u.LogUploadId == id && u.DiscordId == discordId);
        if (upload == null)
        {
            return Results.NotFound();
        }
        if (string.IsNullOrEmpty(upload.DpsReportUrl))
        {
            return Results.BadRequest("No dps.report URL available.");
        }

        pipeline.SubmitToWingman(upload.DpsReportUrl);
        return Results.Ok();
    }

    private static async Task<IResult> SubmitBulkToWingman(
        ClaimsPrincipal user,
        IDbContextFactory<DatabaseContext> dbContextFactory,
        LogUploadPipelineService pipeline)
    {
        var discordIdStr = user.FindFirst("discord_id")?.Value;
        if (!long.TryParse(discordIdStr, out var discordId))
        {
            return Results.Unauthorized();
        }

        var cutoff = DateTime.UtcNow.AddHours(-24);

        await using var ctx = await dbContextFactory.CreateDbContextAsync();
        var uploads = await ctx.LogUpload
            .Where(u => u.DiscordId == discordId && u.Status == "complete" && u.CreatedAt >= cutoff && u.DpsReportUrl != null)
            .Select(u => u.DpsReportUrl!)
            .ToListAsync();

        foreach (var url in uploads)
        {
            pipeline.SubmitToWingman(url);
        }

        return Results.Ok(new { submitted = uploads.Count });
    }

    private static async Task<IResult> GetHistory(
        ClaimsPrincipal user,
        IDbContextFactory<DatabaseContext> dbContextFactory,
        int page = 1,
        int pageSize = 20)
    {
        var discordIdStr = user.FindFirst("discord_id")?.Value;
        if (!long.TryParse(discordIdStr, out var discordId))
        {
            return Results.Unauthorized();
        }

        pageSize = Math.Clamp(pageSize, 1, 100);
        page = Math.Max(1, page);

        var cutoff = DateTime.UtcNow.AddHours(-24);

        await using var ctx = await dbContextFactory.CreateDbContextAsync();

        var query = ctx.LogUpload
            .Where(u => u.DiscordId == discordId && u.Status == "complete" && u.CreatedAt >= cutoff);

        var total = await query.CountAsync();

        var items = await query
            .OrderByDescending(u => u.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(u => new
            {
                u.LogUploadId,
                u.FileName,
                u.SourceType,
                u.DpsReportUrl,
                u.FightLogId,
                u.CreatedAt
            })
            .ToListAsync();

        return Results.Ok(new { total, page, pageSize, items });
    }

    private static async Task AuthorizeTusRequestAsync(AuthorizeContext ctx)
    {
        if (ctx.Intent == IntentType.GetOptions)
        {
            return;
        }

        var identityResult = await ResolveTusUploadIdentityAsync(ctx.HttpContext, ctx.HttpContext.RequestAborted);
        if (identityResult.Identity is not { } identity)
        {
            ctx.FailRequest(
                identityResult.FailureStatus ?? HttpStatusCode.Unauthorized,
                identityResult.FailureMessage ?? "Unauthorized.");
            return;
        }

        if (ctx.Intent == IntentType.CreateFile || ctx.Intent == IntentType.ConcatenateFiles)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(ctx.FileId))
        {
            ctx.FailRequest(HttpStatusCode.Forbidden, "You do not own this upload.");
            return;
        }

        var dbFactory = ctx.HttpContext.RequestServices.GetRequiredService<IDbContextFactory<DatabaseContext>>();
        if (!await IsTusUploadOwnerAsync(dbFactory, ctx.FileId, identity.DiscordId, ctx.HttpContext.RequestAborted))
        {
            ctx.FailRequest(HttpStatusCode.Forbidden, "You do not own this upload.");
        }
    }

    // ASP.NET Core model binding instantiates this request DTO.
    // ReSharper disable ClassNeverInstantiated.Local
    // ReSharper disable UnusedAutoPropertyAccessor.Local
    private sealed class SubmitUrlsRequest
    {
        public string[]? Urls { get; init; }

        public bool Wingman { get; init; } = true;
    }

    private sealed class Gw2UploadGuildsRequest
    {
        public string? ApiKey { get; init; }
    }

    private sealed class SubmitGw2UrlRequest
    {
        public string? Url { get; init; }

        public string? GuildId { get; init; }

        public SubmitDiscordDeliveryRequest? DiscordDelivery { get; init; }

        [System.Text.Json.Serialization.JsonExtensionData]
        public Dictionary<string, System.Text.Json.JsonElement>? AdditionalProperties { get; init; }
    }

    private sealed class SubmitDiscordDeliveryRequest
    {
        public string? Mode { get; init; }

        public System.Text.Json.JsonElement? ChannelId { get; init; }

        [System.Text.Json.Serialization.JsonExtensionData]
        public Dictionary<string, System.Text.Json.JsonElement>? AdditionalProperties { get; init; }
    }

    private sealed record Gw2UrlResponse(
        long UploadId,
        long? FightLogId,
        string Status,
        bool Duplicate,
        bool DiscordDeliveryAccepted,
        DiscordDeliveryResult? DiscordDelivery)
    {
        public static Gw2UrlResponse From(
            LogUpload upload,
            bool duplicate,
            DiscordDeliveryResult? discordDelivery) =>
            new(
                upload.LogUploadId,
                upload.FightLogId is > 0 ? upload.FightLogId : null,
                upload.Status,
                duplicate,
                upload.DiscordDeliveryMode is not null,
                discordDelivery);
    }

    private sealed record Gw2UrlErrorResponse(string Error);

    private sealed class Gw2UploadGuildsResponse(string accountName, IReadOnlyList<GuildSummaryDto> guilds)
    {
        public string AccountName { get; } = accountName;

        public IReadOnlyList<string> Capabilities { get; } = ["discord-summary-delivery-v1"];

        public IReadOnlyList<GuildSummaryDto> Guilds { get; } = guilds;
    }

    private sealed class GuildSummaryDto(
        string guildId,
        string guildName,
        DiscordDeliveryCapabilitiesDto discordDelivery)
    {
        public string GuildId { get; } = guildId;

        public string GuildName { get; } = guildName;

        public DiscordDeliveryCapabilitiesDto DiscordDelivery { get; } = discordDelivery;
    }

    private sealed record DiscordDeliveryCapabilitiesDto(
        bool Enabled,
        bool DefaultsAvailable,
        bool ChannelOverrideAllowed,
        IReadOnlyList<string> EnabledMessageKinds,
        IReadOnlyList<DiscordChannelDto> Channels);

    private sealed record DiscordChannelDto(string ChannelId, string ChannelName);

    private sealed record TusUploadIdentity(long DiscordId, IReadOnlySet<long>? AllowedGuildIds);

    private sealed record TusUploadIdentityResult(
        TusUploadIdentity? Identity,
        HttpStatusCode? FailureStatus,
        string? FailureMessage)
    {
        public static TusUploadIdentityResult Success(TusUploadIdentity identity) => new(identity, null, null);

        public static TusUploadIdentityResult Failed(HttpStatusCode status, string message) => new(null, status, message);
    }

    private sealed record Gw2UploadAccess(long DiscordId, string AccountName, IReadOnlyList<GuildSummaryDto> Guilds);

    private sealed record Gw2UploadAccessResult(
        Gw2UploadAccess? Access,
        HttpStatusCode? FailureStatus,
        string? FailureMessage)
    {
        public static Gw2UploadAccessResult Success(Gw2UploadAccess access) => new(access, null, null);

        public static Gw2UploadAccessResult Failed(HttpStatusCode status, string message) => new(null, status, message);
    }

    private sealed record Gw2AccountResult(
        GuildWars2AccountDataModel? Access,
        HttpStatusCode? FailureStatus,
        string? FailureMessage)
    {
        public static Gw2AccountResult Success(GuildWars2AccountDataModel account) => new(account, null, null);

        public static Gw2AccountResult Failed(HttpStatusCode status, string message) => new(null, status, message);
    }
    // ReSharper restore UnusedAutoPropertyAccessor.Local
    // ReSharper restore ClassNeverInstantiated.Local
}
