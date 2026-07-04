using System.Net;
using System.Security.Claims;
using System.Text;
using DonBot.Api.Services;
using DonBot.Core.Models.Entities;
using DonBot.Core.Services.GuildWars2;
using Microsoft.EntityFrameworkCore;
using tusdotnet;
using tusdotnet.Interfaces;
using tusdotnet.Models;
using tusdotnet.Models.Configuration;
using tusdotnet.Stores;

namespace DonBot.Api.Endpoints;

public static class UploadEndpoints
{
    public static void MapUploadEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/upload").RequireAuthorization();
        group.MapPost("/urls", SubmitUrls);
        group.MapGet("/history", GetHistory);
        group.MapGet("/stream/{id:long}", StreamProgress).AllowAnonymous();
        group.MapPost("/wingman/{id:long}", SubmitOneToWingman);
        group.MapPost("/wingman/bulk", SubmitBulkToWingman);
        group.MapTus("/tus", _ => BuildTusConfigurationAsync(app));
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

                    var guildService = ctx.HttpContext.RequestServices.GetRequiredService<IUserGuildsService>();
                    var guildResult = await ResolveTusGuildIdAsync(
                        ctx.Metadata,
                        ctx.HttpContext.User,
                        guildService,
                        ctx.CancellationToken);

                    if (guildResult.FailureStatus is { } status)
                    {
                        ctx.FailRequest(status, guildResult.FailureMessage ?? "Invalid guild id.");
                    }
                },
                OnCreateCompleteAsync = async ctx =>
                {
                    var discordIdStr = ctx.HttpContext.User.FindFirst("discord_id")?.Value;
                    if (!long.TryParse(discordIdStr, out var discordId))
                    {
                        return;
                    }

                    var filename = TryGetMetadataString(ctx.Metadata, "filename", out var metadataFileName)
                        ? metadataFileName
                        : "upload.zevtc";
                    var safeName = Path.GetFileName(filename);
                    var wingman = TryGetMetadataString(ctx.Metadata, "wingman", out var wingmanRaw) &&
                        string.Equals(wingmanRaw, "true", StringComparison.OrdinalIgnoreCase);

                    var guildService = ctx.HttpContext.RequestServices.GetRequiredService<IUserGuildsService>();
                    var guildResult = await ResolveTusGuildIdAsync(
                        ctx.Metadata,
                        ctx.HttpContext.User,
                        guildService,
                        ctx.CancellationToken);

                    if (guildResult.FailureStatus is not null)
                    {
                        return;
                    }

                    var dbFactory = ctx.HttpContext.RequestServices
                        .GetRequiredService<IDbContextFactory<DatabaseContext>>();
                    await using var db = await dbFactory.CreateDbContextAsync(ctx.CancellationToken);

                    var upload = new LogUpload
                    {
                        DiscordId = discordId,
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

        if (!TryGetDiscordId(ctx.HttpContext.User, out var discordId))
        {
            ctx.FailRequest(HttpStatusCode.Unauthorized, "Unauthorized.");
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
        if (!await IsTusUploadOwnerAsync(dbFactory, ctx.FileId, discordId, ctx.HttpContext.RequestAborted))
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
    // ReSharper restore UnusedAutoPropertyAccessor.Local
    // ReSharper restore ClassNeverInstantiated.Local
}
