using System.Security.Claims;
using System.Text.RegularExpressions;
using DonBot.Api.Services;
using DonBot.Models.Entities;
using Microsoft.EntityFrameworkCore;

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
    }

    private record SubmitUrlsRequest(List<string> Urls, bool Wingman = true);

    private static async Task<IResult> SubmitUrls(
        SubmitUrlsRequest request,
        ClaimsPrincipal user,
        IDbContextFactory<DatabaseContext> dbContextFactory,
        LogUploadPipelineService pipeline)
    {
        var discordIdStr = user.FindFirst("discord_id")?.Value;
        if (!long.TryParse(discordIdStr, out var discordId))
            return Results.Unauthorized();

        if (request?.Urls == null || request.Urls.Count == 0)
            return Results.BadRequest("No URLs provided.");

        var validUrls = request.Urls
            .Select(u => u.Trim())
            .Where(u => DpsReportUrlPattern.IsMatch(u))
            .Distinct()
            .ToList();

        if (validUrls.Count == 0)
            return Results.BadRequest("No valid dps.report or wvw.report URLs provided.");

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
        if (!long.TryParse(discordIdStr, out var discordId))
            return Results.Unauthorized();

        await using var ctx = await dbContextFactory.CreateDbContextAsync();
        var upload = await ctx.LogUpload.FirstOrDefaultAsync(u => u.LogUploadId == id && u.DiscordId == discordId);
        if (upload == null) return Results.NotFound();
        if (string.IsNullOrEmpty(upload.DpsReportUrl)) return Results.BadRequest("No dps.report URL available.");

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
            return Results.Unauthorized();

        var cutoff = DateTime.UtcNow.AddHours(-24);

        await using var ctx = await dbContextFactory.CreateDbContextAsync();
        var uploads = await ctx.LogUpload
            .Where(u => u.DiscordId == discordId && u.Status == "complete" && u.CreatedAt >= cutoff && u.DpsReportUrl != null)
            .Select(u => u.DpsReportUrl!)
            .ToListAsync();

        foreach (var url in uploads)
            pipeline.SubmitToWingman(url);

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
            return Results.Unauthorized();

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
