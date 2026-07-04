using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Channels;
using DonBot.Core.Models.Entities;
using DonBot.Core.Models.GuildWars2;
using DonBot.Core.Services.GuildWars2;
using DonBot.Services.GuildWarsServices;
using Microsoft.EntityFrameworkCore;

namespace DonBot.Api.Services;

public sealed class LogUploadPipelineService : BackgroundService
{
    private readonly ILogUploadProgressService _progress;
    private readonly IDbContextFactory<DatabaseContext> _dbContextFactory;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<LogUploadPipelineService> _logger;
    private readonly FightLogIngestionService _fightLogIngestionService;

    private readonly Channel<long> _queue = Channel.CreateUnbounded<long>(new UnboundedChannelOptions { SingleReader = true });
    private readonly SemaphoreSlim _concurrency;

    public LogUploadPipelineService(
        ILogUploadProgressService progress,
        IDbContextFactory<DatabaseContext> dbContextFactory,
        IServiceScopeFactory scopeFactory,
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<LogUploadPipelineService> logger,
        FightLogIngestionService fightLogIngestionService)
    {
        this._progress = progress;
        this._dbContextFactory = dbContextFactory;
        this._scopeFactory = scopeFactory;
        this._httpClientFactory = httpClientFactory;
        this._configuration = configuration;
        this._logger = logger;
        this._fightLogIngestionService = fightLogIngestionService;
        var concurrency = configuration.GetValue("Upload:MaxConcurrentProcessing", 3);
        _concurrency = new SemaphoreSlim(concurrency, concurrency);
    }

    public void Enqueue(long uploadId) => _queue.Writer.TryWrite(uploadId);

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        await foreach (var uploadId in _queue.Reader.ReadAllAsync(ct))
        {
            await _concurrency.WaitAsync(ct);
            _ = Task.Run(async () =>
            {
                try { await ProcessUploadAsync(uploadId, ct); }
                finally { _concurrency.Release(); }
            }, ct);
        }
    }

    private async Task ProcessUploadAsync(long uploadId, CancellationToken ct)
    {
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var dataModelGenerationService = scope.ServiceProvider.GetRequiredService<IDataModelGenerationService>();
            var playerService = scope.ServiceProvider.GetRequiredService<IPlayerService>();
            var pointsAwardService = scope.ServiceProvider.GetRequiredService<IPointsAwardService>();

            await using var ctx = await _dbContextFactory.CreateDbContextAsync(ct);
            var upload = await ctx.LogUpload.FirstOrDefaultAsync(u => u.LogUploadId == uploadId, ct);
            if (upload == null)
            {
                return;
            }

            if (upload.SourceType == "url")
            {
                await ProcessUrlUploadAsync(ctx, upload, dataModelGenerationService, playerService, pointsAwardService, ct);
            }
            else
            {
                await ProcessFileUploadAsync(ctx, upload, dataModelGenerationService, playerService, pointsAwardService, ct);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Upload pipeline failed for upload {id}", uploadId);
            await MarkFailedAsync(uploadId, ex.Message, ct);
            _progress.Publish(uploadId, "failed", ex.Message);
            _progress.Complete(uploadId);
        }
    }

    private async Task ProcessUrlUploadAsync(DatabaseContext ctx, LogUpload upload, IDataModelGenerationService dataModelGenerationService, IPlayerService playerService, IPointsAwardService pointsAwardService, CancellationToken ct)
    {
        var uploadId = upload.LogUploadId;
        var url = upload.DpsReportUrl!;

        await UpdateStatus(ctx, upload, "parsing", ct);
        _progress.Publish(uploadId, "parsing", "Fetching log from URL...");

        var model = await dataModelGenerationService.GenerateEliteInsightDataModelFromUrl(url);
        var modelUrl = string.IsNullOrWhiteSpace(model.FightEliteInsightDataModel.Url)
            ? url
            : model.FightEliteInsightDataModel.Url;
        modelUrl = ReportUrlHelper.CanonicalizeReportUrl(modelUrl, requireHttps: true);

        await UpdateStatus(ctx, upload, "saving", ct);
        _progress.Publish(uploadId, "saving", "Saving log data...");

        var fightLogId = await SaveFightLogAsync(model, playerService, pointsAwardService, ct);

        if (upload.SubmitToWingman)
        {
            FireAndForgetWingman(modelUrl);
        }

        await FinalizeAsync(uploadId, modelUrl, fightLogId, ct);
        _progress.Publish(uploadId, "complete", "Done.", modelUrl, fightLogId);
        _progress.Complete(uploadId);
    }

    private async Task ProcessFileUploadAsync(DatabaseContext ctx, LogUpload upload, IDataModelGenerationService dataModelGenerationService, IPlayerService playerService, IPointsAwardService pointsAwardService, CancellationToken ct)
    {
        var uploadId = upload.LogUploadId;
        var storagePath = _configuration["Upload:StoragePath"] ?? "/tmp/donbot/uploads";
        var eiDllPath = _configuration["EliteInsights:DllPath"]
            ?? throw new InvalidOperationException("EliteInsights:DllPath is not configured.");
        var eiOutputBasePath = _configuration["EliteInsights:OutputBasePath"] ?? "/tmp/donbot/ei-output";
        var dpsReportToken = _configuration["DpsReport:UserToken"] ?? string.Empty;

        var evtcPath = Path.Combine(storagePath, uploadId.ToString(), upload.FileName);
        var jobOutputDir = Path.Combine(eiOutputBasePath, uploadId.ToString());

        try
        {
            await UpdateStatus(ctx, upload, "parsing", ct);
            _progress.Publish(uploadId, "parsing", "Running Elite Insights parser...");

            Directory.CreateDirectory(jobOutputDir);
            var tempConfigPath = Path.Combine(jobOutputDir, "ei.conf");
            var configContent = $"""
                OutLocation={jobOutputDir}
                SaveAtOut=false
                ParseCombatReplay=false
                DetailledWvW=true
                UploadToDPSReports=true
                SaveOutTrace=true
                """;
            if (!string.IsNullOrEmpty(dpsReportToken))
            {
                configContent += $"\nDPSReportUserToken={dpsReportToken}";
            }
            await File.WriteAllTextAsync(tempConfigPath, configContent, ct);

            var isExe = eiDllPath.EndsWith(".exe", StringComparison.OrdinalIgnoreCase);
            var psi = new ProcessStartInfo
            {
                FileName = isExe ? eiDllPath : "dotnet",
                Arguments = isExe
                    ? $"-c \"{tempConfigPath}\" \"{evtcPath}\""
                    : $"\"{eiDllPath}\" -c \"{tempConfigPath}\" \"{evtcPath}\"",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            string eiStdout;
            using (var proc = Process.Start(psi)!)
            {
                eiStdout = await proc.StandardOutput.ReadToEndAsync(ct);
                var eiStderr = await proc.StandardError.ReadToEndAsync(ct);
                await proc.WaitForExitAsync(ct);
                if (proc.ExitCode != 0)
                {
                    _logger.LogWarning("EI CLI exited {code} for upload {id}: {err}", proc.ExitCode, uploadId, eiStderr);
                }
            }

            await UpdateStatus(ctx, upload, "uploading", ct);
            _progress.Publish(uploadId, "uploading", "Getting dps.report link...");

            var eiResult = ParseEiStdout(eiStdout);
            var dpsReportUrl = eiResult?.DpsReportLink
                ?? ExtractDpsReportUrl(eiStdout)
                ?? ExtractDpsReportUrlFromLogFiles(jobOutputDir)
                ?? await FallbackUploadToDpsReportAsync(evtcPath, upload.FileName, dpsReportToken, ct);

            if (dpsReportUrl == null)
            {
                throw new InvalidOperationException("Could not get a dps.report URL from EI output or direct upload.");
            }

            _progress.Publish(uploadId, "uploading", "Got dps.report link.", dpsReportUrl);
            if (upload.SubmitToWingman)
            {
                FireAndForgetWingman(dpsReportUrl);
            }

            await UpdateStatus(ctx, upload, "saving", ct);
            _progress.Publish(uploadId, "saving", "Saving log data...");

            var model = await dataModelGenerationService.GenerateEliteInsightDataModelFromUrl(dpsReportUrl);
            var modelUrl = string.IsNullOrWhiteSpace(model.FightEliteInsightDataModel.Url)
                ? dpsReportUrl
                : model.FightEliteInsightDataModel.Url;
            modelUrl = ReportUrlHelper.CanonicalizeReportUrl(modelUrl, requireHttps: true);
            var fightLogId = await SaveFightLogAsync(model, playerService, pointsAwardService, ct, upload.GuildId);

            await FinalizeAsync(uploadId, modelUrl, fightLogId, ct);
            _progress.Publish(uploadId, "complete", "Done.", modelUrl, fightLogId);
            _progress.Complete(uploadId);
        }
        finally
        {
            Cleanup(evtcPath, jobOutputDir);
        }
    }

    private static EiProcessedResult? ParseEiStdout(string stdout)
    {
        var line = stdout.Split('\n')
            .Select(l => l.Trim())
            .FirstOrDefault(l => l.StartsWith("Processed - {"));
        if (line == null)
        {
            return null;
        }

        var json = line["Processed - ".Length..];
        return JsonSerializer.Deserialize<EiProcessedResult>(json);
    }

    private static string? ExtractDpsReportUrl(string text)
    {
        return ReportUrlHelper.ExtractFirstReportUrl(text, requireHttps: false);
    }

    private static string? ExtractDpsReportUrlFromLogFiles(string outputDir)
    {
        foreach (var logFile in Directory.GetFiles(outputDir, "*.log"))
        {
            var url = ExtractDpsReportUrl(File.ReadAllText(logFile));
            if (url != null)
            {
                return url;
            }
        }
        return null;
    }

    private sealed class EiProcessedResult
    {
        [JsonPropertyName("generatedFiles")]
        public List<string>? GeneratedFiles { get; init; }

        [JsonPropertyName("dpsReportLink")]
        public string? DpsReportLink { get; init; }

        [JsonPropertyName("dpsReportUploadTentative")]
        public bool DpsReportUploadTentative { get; init; }

        [JsonPropertyName("dpsReportUploadFailed")]
        public bool DpsReportUploadFailed { get; init; }
    }

    private async Task<string?> FallbackUploadToDpsReportAsync(string filePath, string fileName, string userToken, CancellationToken ct)
    {
        _logger.LogInformation("EI did not output a dps.report URL, falling back to direct upload for {file}", fileName);

        var uploadUrl = string.IsNullOrEmpty(userToken)
            ? "https://dps.report/uploadContent?json=1&generator=ei"
            : $"https://dps.report/uploadContent?json=1&generator=ei&userToken={Uri.EscapeDataString(userToken)}";

        var delays = new[] { 5, 15, 30 };
        for (var attempt = 0; attempt <= delays.Length; attempt++)
        {
            try
            {
                await using var fileStream = File.OpenRead(filePath);
                using var form = new MultipartFormDataContent();
                form.Add(new StreamContent(fileStream), "file", fileName);

                var client = _httpClientFactory.CreateClient();
                client.Timeout = TimeSpan.FromMinutes(5);
                var response = await client.PostAsync(uploadUrl, form, ct);

                if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests && attempt < delays.Length)
                {
                    _logger.LogWarning("dps.report rate limited for {file}, retrying in {s}s (attempt {a}/{t})", fileName, delays[attempt], attempt + 1, delays.Length);
                    await Task.Delay(TimeSpan.FromSeconds(delays[attempt]), ct);
                    continue;
                }

                response.EnsureSuccessStatusCode();
                var json = await response.Content.ReadAsStringAsync(ct);
                return JsonSerializer.Deserialize<DpsReportUploadResult>(json)?.Permalink;
            }
            catch (Exception ex) when (attempt < delays.Length && ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "Fallback dps.report upload failed for {file} (attempt {a}/{t}), retrying in {s}s", fileName, attempt + 1, delays.Length, delays[attempt]);
                await Task.Delay(TimeSpan.FromSeconds(delays[attempt]), ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Fallback dps.report upload failed for {file}", fileName);
                return null;
            }
        }

        return null;
    }

    public void SubmitToWingman(string dpsReportUrl) => FireAndForgetWingman(dpsReportUrl);

    private void FireAndForgetWingman(string dpsReportUrl)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                var reportUrl = ReportUrlHelper.CanonicalizeReportUrl(dpsReportUrl, requireHttps: true);
                var client = _httpClientFactory.CreateClient();
                var wingmanUrl = $"https://gw2wingman.nevermindcreations.de/api/importLogQueued?link={Uri.EscapeDataString(reportUrl)}";
                await client.GetAsync(wingmanUrl);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Wingman fire-and-forget failed for {url}", dpsReportUrl);
            }
        });
    }

    private async Task FinalizeAsync(long uploadId, string? dpsReportUrl, long? fightLogId, CancellationToken ct)
    {
        await using var ctx = await _dbContextFactory.CreateDbContextAsync(ct);
        var upload = await ctx.LogUpload.FirstOrDefaultAsync(u => u.LogUploadId == uploadId, ct);
        if (upload == null)
        {
            return;
        }
        upload.Status = "complete";
        upload.DpsReportUrl = dpsReportUrl;
        upload.FightLogId = fightLogId;
        upload.UpdatedAt = DateTime.UtcNow;
        ctx.LogUpload.Update(upload);
        await ctx.SaveChangesAsync(ct);
    }

    private async Task MarkFailedAsync(long uploadId, string message, CancellationToken ct)
    {
        try
        {
            await using var ctx = await _dbContextFactory.CreateDbContextAsync(ct);
            var upload = await ctx.LogUpload.FirstOrDefaultAsync(u => u.LogUploadId == uploadId, ct);
            if (upload == null)
            {
                return;
            }
            upload.Status = "failed";
            upload.ErrorMessage = message[..Math.Min(message.Length, 2000)];
            upload.UpdatedAt = DateTime.UtcNow;
            ctx.LogUpload.Update(upload);
            await ctx.SaveChangesAsync(ct);
        }
        catch { /* best effort */ }
    }

    private static async Task UpdateStatus(DatabaseContext ctx, LogUpload upload, string status, CancellationToken ct)
    {
        upload.Status = status;
        upload.UpdatedAt = DateTime.UtcNow;
        ctx.LogUpload.Update(upload);
        await ctx.SaveChangesAsync(ct);
    }

    private async Task<long> SaveFightLogAsync(EliteInsightDataModel data, IPlayerService playerService, IPointsAwardService pointsAwardService, CancellationToken ct, long guildId = 0)
    {
        var fightPhase = FightLogMaterializer.ResolveFightPhase(data);
        var gw2Players = playerService.GetGw2Players(data, fightPhase, FightLogMaterializer.ShouldSumAllTargets(data));
        var result = await _fightLogIngestionService.IngestAsync(new FightLogIngestionRequest(data, fightPhase, gw2Players)
        {
            GuildId = guildId,
            ExistingLogUpdateMode = ExistingFightLogUpdateMode.AttachGuildAndRawData,
            SourceFallback = "upload"
        }, ct);

        await pointsAwardService.AwardFightAsync(result.FightLogId, ct);
        return result.FightLogId;
    }

    private static void Cleanup(string? evtcPath, string? jobOutputDir)
    {
        try
        {
            var evtcDir = evtcPath != null ? Path.GetDirectoryName(evtcPath) : null;
            if (evtcDir != null && Directory.Exists(evtcDir))
            {
                Directory.Delete(evtcDir, recursive: true);
            }
        }
        catch { /* best effort */ }

        try
        {
            if (jobOutputDir != null && Directory.Exists(jobOutputDir))
            {
                Directory.Delete(jobOutputDir, recursive: true);
            }
        }
        catch { /* best effort */ }
    }

    private sealed class DpsReportUploadResult
    {
        [JsonPropertyName("permalink")]
        public string? Permalink { get; init; }
    }
}
