using System.Net;
using System.Security.Claims;
using System.Text;
using DonBot.Api.Services;
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
    private const string Gw2ApiKeyHeader = "X-GW2-API-Key";
    private const string TusUploadIdentityItemKey = "donbot:tus-upload-identity";

    public static void MapUploadEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/upload");
        group.MapPost("/urls", SubmitUrls).RequireAuthorization();
        group.MapGet("/history", GetHistory).RequireAuthorization();
        group.MapGet("/stream/{id:long}", StreamProgress).AllowAnonymous();
        group.MapPost("/wingman/{id:long}", SubmitOneToWingman).RequireAuthorization();
        group.MapPost("/wingman/bulk", SubmitBulkToWingman).RequireAuthorization();
        group.MapPost("/gw2/guilds", ListGw2UploadGuilds).AllowAnonymous();
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
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    };
                    db.LogUpload.Add(upload);
                    await db.SaveChangesAsync(ctx.CancellationToken);

                    ctx.HttpContext.Response.Headers["X-Log-Upload-Id"] = upload.LogUploadId.ToString();
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
                ct);

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
        CancellationToken ct)
    {
        var configuredGuilds = await context.Guild
            .AsNoTracking()
            .Select(g => new
            {
                g.GuildId,
                g.GuildName
            })
            .ToListAsync(ct);

        var configuredGuildIds = configuredGuilds
            .Select(g => g.GuildId)
            .ToArray();
        var memberGuildIds = await guildMembershipService.GetMemberGuildIdsAsync(discordId, configuredGuildIds, ct);

        return configuredGuilds
            .Where(g => memberGuildIds.Contains(g.GuildId))
            .OrderBy(g => g.GuildName ?? g.GuildId.ToString())
            .Select(g => new GuildSummaryDto(g.GuildId.ToString(), g.GuildName ?? g.GuildId.ToString()))
            .ToList();
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

    private static async Task<IResult> ListGw2UploadGuilds(
        Gw2UploadGuildsRequest request,
        IDbContextFactory<DatabaseContext> dbContextFactory,
        IHttpClientFactory httpClientFactory,
        IDiscordGuildMembershipService guildMembershipService,
        CancellationToken ct)
    {
        var result = await ResolveGw2UploadAccessAsync(
            request.ApiKey,
            dbContextFactory,
            httpClientFactory,
            guildMembershipService,
            ct);
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
        HttpContext ctx,
        CancellationToken ct)
    {
        SseWriter.Prepare(ctx.Response);

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

    private sealed class Gw2UploadGuildsResponse(string accountName, IReadOnlyList<GuildSummaryDto> guilds)
    {
        public string AccountName { get; } = accountName;

        public IReadOnlyList<GuildSummaryDto> Guilds { get; } = guilds;
    }

    private sealed class GuildSummaryDto(string guildId, string guildName)
    {
        public string GuildId { get; } = guildId;

        public string GuildName { get; } = guildName;
    }

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
