using DonBot.Api.Services;
using DonBot.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Serilog.Core;
using System.Net;
using System.Security.Claims;
using System.Text;
using System.Text.RegularExpressions;
using tusdotnet;
using tusdotnet.Interfaces;
using tusdotnet.Models;
using tusdotnet.Models.Configuration;
using tusdotnet.Stores;

namespace DonBot.Api.Endpoints;

public static class UploadEndpoints
{
    private static readonly Regex DpsReportUrlPattern =
        new(@"^https?://(b\.dps|dps|wvw)\.report/\S+$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public static void MapUploadEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/upload").RequireAuthorization();
        group.MapPost("/urls", SubmitUrls);
        group.MapGet("/history", GetHistory);
        group.MapGet("/stream/{id:long}", StreamProgress).AllowAnonymous();
        group.MapPost("/wingman/{id:long}", SubmitOneToWingman);
        group.MapPost("/wingman/bulk", SubmitBulkToWingman);

        group.MapTus("/tus", async httpContext =>
        {
            var guildService = httpContext.RequestServices.GetRequiredService<IUserGuildsService>();
            var storagePath = app.Configuration["Upload:StoragePath"] ?? "/tmp/donbot/uploads";
            var tusTempPath = Path.Combine(storagePath, "tus-temp");
            Directory.CreateDirectory(tusTempPath);

            return new DefaultTusConfiguration
            {
                Store = new TusDiskStore(tusTempPath),
                Events = new Events
                {
                    OnAuthorizeAsync = ctx =>
                    {
                        if (ctx.Intent == IntentType.GetOptions)
                        {
                            return Task.CompletedTask;
                        }
                        if (!(ctx.HttpContext.User.Identity?.IsAuthenticated ?? false))
                        {
                            ctx.FailRequest(HttpStatusCode.Unauthorized, "Unauthorized.");
                        }
                        return Task.CompletedTask;
                    },
                    OnBeforeCreateAsync = ctx =>
                    {
                        if (!ctx.Metadata.TryGetValue("filename", out var fn) ||
                            !fn.GetString(Encoding.UTF8).EndsWith(".zevtc", StringComparison.OrdinalIgnoreCase))
                        {
                            ctx.FailRequest(HttpStatusCode.BadRequest, "Only .zevtc files are allowed.");
                        }
                        return Task.CompletedTask;
                    },
                    OnCreateCompleteAsync = async ctx =>
                    {
                        var discordIdStr = ctx.HttpContext.User.FindFirst("discord_id")?.Value;
                        if (!long.TryParse(discordIdStr, out var discordId))
                        {
                            return;
                        }

                        var filename = ctx.Metadata.TryGetValue("filename", out var fn)
                            ? fn.GetString(Encoding.UTF8)
                            : "upload.zevtc";
                        var safeName = Path.GetFileName(filename);
                        var wingman = ctx.Metadata.TryGetValue("wingman", out var wm) &&
                            wm.GetString(Encoding.UTF8) == "true";
                        long guildId = ctx.Metadata.TryGetValue("guildid", out var gid)
                            ? long.TryParse(gid.GetString(Encoding.UTF8), out var gidParsed)
                                ? gidParsed
                                : 0
                            : 0;

                        // Make sure user is part of guild they are uploading for
                        if (ctx.HttpContext.User != null)
                        {
                            var userGuildList = await guildService.GetForPrincipalAsync(ctx.HttpContext.User);
                            if (userGuildList == null || userGuildList.First(guild => (long)guild.Id == guildId) == null)
                            {
                                // User is not part of the guild they are trying to upload for
                                //logger.LogWarning("User tried uploading a log for guild they are not in: {id}", uploadId);
                                guildId = 0;
                            }
                        }
                        else
                        {
                            //logger.LogWarning("User tried uploading a log for guild they are not in: {id}", uploadId);
                            guildId = 0;
                        }

                        var dbFactory = ctx.HttpContext.RequestServices
                            .GetRequiredService<IDbContextFactory<DatabaseContext>>();
                        await using var db = await dbFactory.CreateDbContextAsync();

                        var upload = new LogUpload
                        {
                            DiscordId = discordId,
                            FileName = safeName,
                            SourceType = "file",
                            Status = "receiving",
                            SubmitToWingman = wingman,
                            GuildId = guildId,
                            CreatedAt = DateTime.UtcNow,
                            UpdatedAt = DateTime.UtcNow
                        };
                        db.LogUpload.Add(upload);
                        await db.SaveChangesAsync();

                        ctx.HttpContext.RequestServices.GetRequiredService<TusFileMapping>()
                            .Add(ctx.FileId, upload.LogUploadId);

                        ctx.HttpContext.Response.Headers["X-Log-Upload-Id"] = upload.LogUploadId.ToString();
                    },
                    OnFileCompleteAsync = async ctx =>
                    {
                        var mapping = ctx.HttpContext.RequestServices.GetRequiredService<TusFileMapping>();
                        if (!mapping.TryRemove(ctx.FileId, out var logUploadId))
                        {
                            return;
                        }

                        var storagePath2 = ctx.HttpContext.RequestServices
                            .GetRequiredService<IConfiguration>()["Upload:StoragePath"] ?? "/tmp/donbot/uploads";

                        var dbFactory = ctx.HttpContext.RequestServices
                            .GetRequiredService<IDbContextFactory<DatabaseContext>>();
                        await using var db = await dbFactory.CreateDbContextAsync();
                        var upload = await db.LogUpload.FirstOrDefaultAsync(
                            u => u.LogUploadId == logUploadId, ctx.CancellationToken);
                        if (upload == null)
                        {
                            return;
                        }

                        var uploadDir = Path.Combine(storagePath2, logUploadId.ToString());
                        Directory.CreateDirectory(uploadDir);

                        var file = await ctx.GetFileAsync();
                        await using (var content = await file.GetContentAsync(ctx.CancellationToken))
                        await using (var dest = File.Create(Path.Combine(uploadDir, upload.FileName)))
                            await content.CopyToAsync(dest, ctx.CancellationToken);

                        if (ctx.Store is ITusTerminationStore terminationStore)
                        {
                            await terminationStore.DeleteFileAsync(ctx.FileId, ctx.CancellationToken);
                        }

                        upload.Status = "stored";
                        upload.UpdatedAt = DateTime.UtcNow;
                        db.LogUpload.Update(upload);
                        await db.SaveChangesAsync();

                        ctx.HttpContext.RequestServices.GetRequiredService<LogUploadPipelineService>()
                            .Enqueue(logUploadId);
                    }
                }
            };
        });
    }

    private record SubmitUrlsRequest(List<string> Urls, bool Wingman = true);

    private static async Task<IResult> SubmitUrls(
        SubmitUrlsRequest request,
        ClaimsPrincipal user,
        IDbContextFactory<DatabaseContext> dbContextFactory,
        LogUploadPipelineService pipeline)
    {
        var discordIdStr = user.FindFirst("discord_id")?.Value;
        if (!long.TryParse(discordIdStr, out var discordId)) {
            return Results.Unauthorized();
        }

        if (request?.Urls == null || request.Urls.Count == 0) {
            return Results.BadRequest("No URLs provided.");
        }

        var validUrls = request.Urls
            .Select(u => u.Trim())
            .Where(u => DpsReportUrlPattern.IsMatch(u))
            .Distinct()
            .ToList();

        if (validUrls.Count == 0) {
            return Results.BadRequest("No valid dps.report or wvw.report URLs provided.");
        }

        await using var ctx = await dbContextFactory.CreateDbContextAsync();
        var created = new List<object>();

        foreach (var url in validUrls)
        {
            var displayName = Uri.TryCreate(url, UriKind.Absolute, out var uri)
                ? uri.Segments.LastOrDefault()?.Trim('/') ?? url
                : url;

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
        ctx.Response.ContentType = "text/event-stream";
        ctx.Response.Headers["Cache-Control"] = "no-cache";
        ctx.Response.Headers["X-Accel-Buffering"] = "no";

        await foreach (var msg in progress.Subscribe(id, ct))
        {
            await ctx.Response.WriteAsync($"data: {msg}\n\n", ct);
            await ctx.Response.Body.FlushAsync(ct);
        }
    }

    private static async Task<IResult> SubmitOneToWingman(
        long id,
        ClaimsPrincipal user,
        IDbContextFactory<DatabaseContext> dbContextFactory,
        LogUploadPipelineService pipeline)
    {
        var discordIdStr = user.FindFirst("discord_id")?.Value;
        if (!long.TryParse(discordIdStr, out var discordId)) {
            return Results.Unauthorized();
        }

        await using var ctx = await dbContextFactory.CreateDbContextAsync();
        var upload = await ctx.LogUpload.FirstOrDefaultAsync(u => u.LogUploadId == id && u.DiscordId == discordId);
        if (upload == null) {
            return Results.NotFound();
        }
        if (string.IsNullOrEmpty(upload.DpsReportUrl)) {
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
        if (!long.TryParse(discordIdStr, out var discordId)) {
            return Results.Unauthorized();
        }

        var cutoff = DateTime.UtcNow.AddHours(-24);

        await using var ctx = await dbContextFactory.CreateDbContextAsync();
        var uploads = await ctx.LogUpload
            .Where(u => u.DiscordId == discordId && u.Status == "complete" && u.CreatedAt >= cutoff && u.DpsReportUrl != null)
            .Select(u => u.DpsReportUrl!)
            .ToListAsync();

        foreach (var url in uploads) {
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
        if (!long.TryParse(discordIdStr, out var discordId)) {
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
}
