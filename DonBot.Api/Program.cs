using System.Net;
using System.Text;
using DonBot.Api.Endpoints;
using DonBot.Api.Registration;
using DonBot.Api.Services;
using DonBot.Models.Entities;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.EntityFrameworkCore;
using Serilog;
using tusdotnet;
using tusdotnet.Interfaces;
using tusdotnet.Models;
using tusdotnet.Models.Configuration;
using tusdotnet.Stores;

var envFile = Path.Combine(Directory.GetCurrentDirectory(), "..", ".env");
if (File.Exists(envFile))
    DotNetEnv.Env.Load(envFile);

var builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddJsonFile("appsettings.user.json", optional: true);

builder.Host.UseSerilog((context, services, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration)
    .ReadFrom.Services(services));

ApiServiceRegister.ConfigureServices(builder.Services, builder.Configuration);

var maxUploadBytes = builder.Configuration.GetValue<long>("Upload:MaxRequestBytes", 1_073_741_824);
builder.Services.Configure<FormOptions>(o => o.MultipartBodyLengthLimit = maxUploadBytes);
builder.WebHost.ConfigureKestrel(o => o.Limits.MaxRequestBodySize = maxUploadBytes);

var app = builder.Build();

app.UseSerilogRequestLogging();
app.UseCors("DonBotPolicy");
app.UseAuthentication();
app.UseAuthorization();

app.MapAuthEndpoints();
app.MapLogsEndpoints();
app.MapUploadEndpoints();
app.MapStatsEndpoints();
app.MapLeaderboardEndpoints();
app.MapPointsEndpoints();
app.MapAccountEndpoints();
app.MapGuildAdminEndpoints();

app.MapTus("/api/upload/tus", async httpContext =>
{
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
                if (ctx.Intent == IntentType.GetOptions) return Task.CompletedTask;
                if (!(ctx.HttpContext.User.Identity?.IsAuthenticated ?? false))
                    ctx.FailRequest(HttpStatusCode.Unauthorized, "Unauthorized.");
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
                if (!long.TryParse(discordIdStr, out var discordId)) return;

                var filename = ctx.Metadata.TryGetValue("filename", out var fn)
                    ? fn.GetString(Encoding.UTF8)
                    : "upload.zevtc";
                var safeName = Path.GetFileName(filename);
                var wingman = ctx.Metadata.TryGetValue("wingman", out var wm) &&
                    wm.GetString(Encoding.UTF8) == "true";

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
                if (!mapping.TryRemove(ctx.FileId, out var logUploadId)) return;

                var storagePath2 = ctx.HttpContext.RequestServices
                    .GetRequiredService<IConfiguration>()["Upload:StoragePath"] ?? "/tmp/donbot/uploads";

                var dbFactory = ctx.HttpContext.RequestServices
                    .GetRequiredService<IDbContextFactory<DatabaseContext>>();
                await using var db = await dbFactory.CreateDbContextAsync();
                var upload = await db.LogUpload.FirstOrDefaultAsync(
                    u => u.LogUploadId == logUploadId, ctx.CancellationToken);
                if (upload == null) return;

                var uploadDir = Path.Combine(storagePath2, logUploadId.ToString());
                Directory.CreateDirectory(uploadDir);

                var file = await ctx.GetFileAsync();
                await using (var content = await file.GetContentAsync(ctx.CancellationToken))
                await using (var dest = File.Create(Path.Combine(uploadDir, upload.FileName)))
                    await content.CopyToAsync(dest, ctx.CancellationToken);

                if (ctx.Store is ITusTerminationStore terminationStore)
                    await terminationStore.DeleteFileAsync(ctx.FileId, ctx.CancellationToken);

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

app.Run();
