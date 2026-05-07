using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Threading.Channels;
using DonBot.Models.Entities;
using DonBot.Models.Enums;
using DonBot.Services;
using DonBot.Models.GuildWars2;
using DonBot.Services.GuildWarsServices;
using Microsoft.EntityFrameworkCore;

namespace DonBot.Api.Services;

public sealed class LogUploadPipelineService : BackgroundService
{
    private readonly ILogUploadProgressService progress;
    private readonly IDbContextFactory<DatabaseContext> dbContextFactory;
    private readonly IServiceScopeFactory scopeFactory;
    private readonly IHttpClientFactory httpClientFactory;
    private readonly IConfiguration configuration;
    private readonly ILogger<LogUploadPipelineService> logger;

    private static readonly Regex DpsReportUrlPattern =
        new(@"https?://(b\.dps|dps|wvw)\.report/\S+", RegexOptions.Compiled);

    private readonly Channel<long> _queue = Channel.CreateUnbounded<long>(new UnboundedChannelOptions { SingleReader = true });
    private readonly SemaphoreSlim _concurrency;

    public LogUploadPipelineService(
        ILogUploadProgressService progress,
        IDbContextFactory<DatabaseContext> dbContextFactory,
        IServiceScopeFactory scopeFactory,
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<LogUploadPipelineService> logger)
    {
        this.progress = progress;
        this.dbContextFactory = dbContextFactory;
        this.scopeFactory = scopeFactory;
        this.httpClientFactory = httpClientFactory;
        this.configuration = configuration;
        this.logger = logger;
        var concurrency = configuration.GetValue<int>("Upload:MaxConcurrentProcessing", 3);
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
            await using var scope = scopeFactory.CreateAsyncScope();
            var dataModelGenerationService = scope.ServiceProvider.GetRequiredService<IDataModelGenerationService>();
            var playerService = scope.ServiceProvider.GetRequiredService<IPlayerService>();

            await using var ctx = await dbContextFactory.CreateDbContextAsync(ct);
            var upload = await ctx.LogUpload.FirstOrDefaultAsync(u => u.LogUploadId == uploadId, ct);
            if (upload == null) return;

            if (upload.SourceType == "url")
                await ProcessUrlUploadAsync(ctx, upload, dataModelGenerationService, playerService, ct);
            else
                await ProcessFileUploadAsync(ctx, upload, dataModelGenerationService, playerService, ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Upload pipeline failed for upload {id}", uploadId);
            await MarkFailedAsync(uploadId, ex.Message, ct);
            progress.Publish(uploadId, "failed", ex.Message);
            progress.Complete(uploadId);
        }
    }

    private async Task ProcessUrlUploadAsync(DatabaseContext ctx, LogUpload upload, IDataModelGenerationService dataModelGenerationService, IPlayerService playerService, CancellationToken ct)
    {
        var uploadId = upload.LogUploadId;
        var url = upload.DpsReportUrl!;

        var existingLog = await ctx.FightLog.FirstOrDefaultAsync(f => f.Url == url, ct);
        if (existingLog != null)
        {
            await FinalizeAsync(uploadId, url, existingLog.FightLogId, ct);
            progress.Publish(uploadId, "complete", "Already saved.", url, existingLog.FightLogId);
            progress.Complete(uploadId);
            return;
        }

        await UpdateStatus(ctx, upload, "parsing", ct);
        progress.Publish(uploadId, "parsing", "Fetching log from URL...");

        var model = await dataModelGenerationService.GenerateEliteInsightDataModelFromUrl(url);

        await UpdateStatus(ctx, upload, "saving", ct);
        progress.Publish(uploadId, "saving", "Saving log data...");

        var fightLogId = await SaveFightLogAsync(model, playerService, ct);

        if (upload.SubmitToWingman) FireAndForgetWingman(url);

        await FinalizeAsync(uploadId, url, fightLogId, ct);
        progress.Publish(uploadId, "complete", "Done.", url, fightLogId);
        progress.Complete(uploadId);
    }

    private async Task ProcessFileUploadAsync(DatabaseContext ctx, LogUpload upload, IDataModelGenerationService dataModelGenerationService, IPlayerService playerService, CancellationToken ct)
    {
        var uploadId = upload.LogUploadId;
        var storagePath = configuration["Upload:StoragePath"] ?? "/tmp/donbot/uploads";
        var eiDllPath = configuration["EliteInsights:DllPath"]
            ?? throw new InvalidOperationException("EliteInsights:DllPath is not configured.");
        var eiOutputBasePath = configuration["EliteInsights:OutputBasePath"] ?? "/tmp/donbot/ei-output";
        var dpsReportToken = configuration["DpsReport:UserToken"] ?? string.Empty;

        var evtcPath = Path.Combine(storagePath, uploadId.ToString(), upload.FileName);
        var jobOutputDir = Path.Combine(eiOutputBasePath, uploadId.ToString());

        try
        {
            // Stage: parsing (EI runs and uploads to dps.report)
            await UpdateStatus(ctx, upload, "parsing", ct);
            progress.Publish(uploadId, "parsing", "Running Elite Insights parser...");

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
                configContent += $"\nDPSReportUserToken={dpsReportToken}";
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
                    logger.LogWarning("EI CLI exited {code} for upload {id}: {err}", proc.ExitCode, uploadId, eiStderr);
            }

            // Stage: uploading - extract URL from EI stdout JSON, fallback to uploading ourselves
            await UpdateStatus(ctx, upload, "uploading", ct);
            progress.Publish(uploadId, "uploading", "Getting dps.report link...");

            var eiResult = ParseEiStdout(eiStdout);
            var htmlPath = eiResult?.GeneratedFiles?.FirstOrDefault(f => f.EndsWith(".html", StringComparison.OrdinalIgnoreCase))
                ?? Directory.GetFiles(jobOutputDir, "*.html").FirstOrDefault();

            var dpsReportUrl = eiResult?.DpsReportLink
                ?? ExtractDpsReportUrl(eiStdout)
                ?? ExtractDpsReportUrlFromLogFiles(jobOutputDir)
                ?? await FallbackUploadToDpsReportAsync(evtcPath, upload.FileName, dpsReportToken, ct);

            if (dpsReportUrl == null)
                throw new InvalidOperationException("Could not get a dps.report URL from EI output or direct upload.");

            progress.Publish(uploadId, "uploading", "Got dps.report link.", dpsReportUrl);
            if (upload.SubmitToWingman) FireAndForgetWingman(dpsReportUrl);

            // Stage: saving - fetch parsed model from dps.report, save to DB
            await UpdateStatus(ctx, upload, "saving", ct);
            progress.Publish(uploadId, "saving", "Saving log data...");

            EliteInsightDataModel model;
            if (htmlPath != null && File.Exists(htmlPath))
            {
                var html = await File.ReadAllTextAsync(htmlPath, ct);
                model = dataModelGenerationService.GenerateEliteInsightDataModelFromHtml(html, dpsReportUrl);
            }
            else
            {
                model = await dataModelGenerationService.GenerateEliteInsightDataModelFromUrl(dpsReportUrl);
            }
            var fightLogId = await SaveFightLogAsync(model, playerService, ct);

            await FinalizeAsync(uploadId, dpsReportUrl, fightLogId, ct);
            progress.Publish(uploadId, "complete", "Done.", dpsReportUrl, fightLogId);
            progress.Complete(uploadId);
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
        if (line == null) return null;

        var json = line["Processed - ".Length..];
        return JsonSerializer.Deserialize<EiProcessedResult>(json);
    }

    private static string? ExtractDpsReportUrl(string text)
    {
        var match = DpsReportUrlPattern.Match(text);
        return match.Success ? match.Value.TrimEnd('.', ',', ')') : null;
    }

    private static string? ExtractDpsReportUrlFromLogFiles(string outputDir)
    {
        foreach (var logFile in Directory.GetFiles(outputDir, "*.log"))
        {
            var url = ExtractDpsReportUrl(File.ReadAllText(logFile));
            if (url != null) return url;
        }
        return null;
    }

    private sealed class EiProcessedResult
    {
        [JsonPropertyName("generatedFiles")]
        public List<string>? GeneratedFiles { get; set; }

        [JsonPropertyName("dpsReportLink")]
        public string? DpsReportLink { get; set; }

        [JsonPropertyName("dpsReportUploadTentative")]
        public bool DpsReportUploadTentative { get; set; }

        [JsonPropertyName("dpsReportUploadFailed")]
        public bool DpsReportUploadFailed { get; set; }
    }

    private async Task<string?> FallbackUploadToDpsReportAsync(string filePath, string fileName, string userToken, CancellationToken ct)
    {
        logger.LogInformation("EI did not output a dps.report URL, falling back to direct upload for {file}", fileName);

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

                var client = httpClientFactory.CreateClient();
                client.Timeout = TimeSpan.FromMinutes(5);
                var response = await client.PostAsync(uploadUrl, form, ct);

                if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests && attempt < delays.Length)
                {
                    logger.LogWarning("dps.report rate limited for {file}, retrying in {s}s (attempt {a}/{t})", fileName, delays[attempt], attempt + 1, delays.Length);
                    await Task.Delay(TimeSpan.FromSeconds(delays[attempt]), ct);
                    continue;
                }

                response.EnsureSuccessStatusCode();
                var json = await response.Content.ReadAsStringAsync(ct);
                return JsonSerializer.Deserialize<DpsReportUploadResult>(json)?.Permalink;
            }
            catch (Exception ex) when (attempt < delays.Length && ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "Fallback dps.report upload failed for {file} (attempt {a}/{t}), retrying in {s}s", fileName, attempt + 1, delays.Length, delays[attempt]);
                await Task.Delay(TimeSpan.FromSeconds(delays[attempt]), ct);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Fallback dps.report upload failed for {file}", fileName);
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
                var client = httpClientFactory.CreateClient();
                var wingmanUrl = $"https://gw2wingman.nevermindcreations.de/api/importLogQueued?link={Uri.EscapeDataString(dpsReportUrl)}";
                await client.GetAsync(wingmanUrl);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Wingman fire-and-forget failed for {url}", dpsReportUrl);
            }
        });
    }

    private async Task FinalizeAsync(long uploadId, string? dpsReportUrl, long? fightLogId, CancellationToken ct)
    {
        await using var ctx = await dbContextFactory.CreateDbContextAsync(ct);
        var upload = await ctx.LogUpload.FirstOrDefaultAsync(u => u.LogUploadId == uploadId, ct);
        if (upload == null) return;
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
            await using var ctx = await dbContextFactory.CreateDbContextAsync(ct);
            var upload = await ctx.LogUpload.FirstOrDefaultAsync(u => u.LogUploadId == uploadId, ct);
            if (upload == null) return;
            upload.Status = "failed";
            upload.ErrorMessage = message[..Math.Min(message.Length, 2000)];
            upload.UpdatedAt = DateTime.UtcNow;
            ctx.LogUpload.Update(upload);
            await ctx.SaveChangesAsync(ct);
        }
        catch { /* best-effort */ }
    }

    private static async Task UpdateStatus(DatabaseContext ctx, LogUpload upload, string status, CancellationToken ct)
    {
        upload.Status = status;
        upload.UpdatedAt = DateTime.UtcNow;
        ctx.LogUpload.Update(upload);
        await ctx.SaveChangesAsync(ct);
    }

    private async Task<long> SaveFightLogAsync(EliteInsightDataModel data, IPlayerService playerService, CancellationToken ct)
    {
        var fightPhase = data.FightEliteInsightDataModel.Phases?.Any() == true
            ? data.FightEliteInsightDataModel.Phases[0]
            : new ArcDpsPhase();

        await using var ctx = await dbContextFactory.CreateDbContextAsync(ct);

        var url = data.FightEliteInsightDataModel.Url;
        FightLog? existing = null;

        if (!string.IsNullOrEmpty(url))
            existing = await ctx.FightLog.FirstOrDefaultAsync(f => f.Url == url, ct);

        if (existing == null)
        {
            var fightStart = ParseStart(data.FightEliteInsightDataModel.Start);
            var fightType = data.FightEliteInsightDataModel.Wvw
                ? (short)FightTypesEnum.WvW
                : GetPvEEncounterType(data.FightEliteInsightDataModel.FightId).encounterType;
            existing = await FightLogDeduplication.FindByContentAsync(
                ctx, fightType, fightStart,
                playerService.GetGw2Players(data, fightPhase).Select(p => p.AccountName), ct);
        }

        return data.FightEliteInsightDataModel.Wvw
            ? await SaveWvWFightLogAsync(ctx, data, fightPhase, existing, playerService, ct)
            : await SavePvEFightLogAsync(ctx, data, fightPhase, existing, playerService, ct);
    }

    private async Task<long> SaveWvWFightLogAsync(DatabaseContext ctx, EliteInsightDataModel data, ArcDpsPhase fightPhase, FightLog? existing, IPlayerService playerService, CancellationToken ct)
    {
        if (existing != null) return existing.FightLogId;

        var dateTimeStart = ParseStart(data.FightEliteInsightDataModel.Start);
        long durationMs;
        var dateEndString = data.FightEliteInsightDataModel.End;
        if (!string.IsNullOrEmpty(dateEndString))
        {
            var dateTimeEnd = DateTime.ParseExact(dateEndString, "yyyy-MM-dd HH:mm:ss zzz", CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal);
            durationMs = (long)(dateTimeEnd - dateTimeStart).TotalMilliseconds;
        }
        else
        {
            durationMs = fightPhase.Duration;
        }

        var fightLog = new FightLog
        {
            GuildId = 0,
            Url = data.FightEliteInsightDataModel.Url,
            FightType = (short)FightTypesEnum.WvW,
            FightStart = dateTimeStart,
            FightDurationInMs = durationMs,
            IsSuccess = data.FightEliteInsightDataModel.Success ?? fightPhase.Success ?? false,
            Source = GetLogSource(data.FightEliteInsightDataModel.Url)
        };
        ctx.FightLog.Add(fightLog);
        await ctx.SaveChangesAsync(ct);

        ctx.FightLogRawData.Add(new FightLogRawData
        {
            FightLogId = fightLog.FightLogId,
            RawFightData = data.RawFightData,
            RawHealingData = data.RawHealingData,
            RawBarrierData = data.RawBarrierData
        });

        var gw2Players = playerService.GetGw2Players(data, fightPhase);
        ctx.PlayerFightLog.AddRange(gw2Players.Select(p => BuildPlayerFightLog(p, fightLog.FightLogId)));
        await ctx.SaveChangesAsync(ct);

        return fightLog.FightLogId;
    }

    private async Task<long> SavePvEFightLogAsync(DatabaseContext ctx, EliteInsightDataModel data, ArcDpsPhase fightPhase, FightLog? existing, IPlayerService playerService, CancellationToken ct)
    {
        if (existing != null) return existing.FightLogId;

        var dateTimeStart = ParseStart(data.FightEliteInsightDataModel.Start);
        var (encounterType, sumAllTargets) = GetPvEEncounterType(data.FightEliteInsightDataModel.FightId);

        var mainTarget = data.FightEliteInsightDataModel.Targets?.FirstOrDefault() ?? new ArcDpsTarget { HpLeft = 1, Health = 1 };
        var fightPercent = Math.Round((mainTarget.HpLeft / (decimal)(mainTarget.Health == 0 ? 1 : mainTarget.Health)) * 100, 2);
        int? fightPhaseCount = null;

        if (encounterType == (short)FightTypesEnum.Ht)
        {
            var finalTarget = data.FightEliteInsightDataModel.Targets?.LastOrDefault(s => s.HbWidth == 800) ?? mainTarget;
            fightPhaseCount = data.FightEliteInsightDataModel.Targets?.Count(s => s.HbWidth == 800);
            fightPercent = Math.Round((finalTarget.HpLeft / (decimal)(finalTarget.Health == 0 ? 1 : finalTarget.Health)) * 100, 2);
        }

        var fightMode = !string.IsNullOrEmpty(data.FightEliteInsightDataModel.FightMode ?? fightPhase.Mode)
            ? data.FightEliteInsightDataModel.GetFightMode()
            : data.FightEliteInsightDataModel.LogName?.Split(' ').LastOrDefault() switch
            {
                "CM" => 1,
                "LCM" => 2,
                _ => 0
            };

        var fightLog = new FightLog
        {
            GuildId = 0,
            Url = data.FightEliteInsightDataModel.Url,
            FightType = encounterType,
            FightStart = dateTimeStart,
            FightDurationInMs = fightPhase.Duration,
            IsSuccess = data.FightEliteInsightDataModel.Success ?? fightPhase.Success ?? false,
            FightPercent = fightPercent,
            FightPhase = fightPhaseCount,
            FightMode = fightMode,
            Source = GetLogSource(data.FightEliteInsightDataModel.Url)
        };
        ctx.FightLog.Add(fightLog);
        await ctx.SaveChangesAsync(ct);

        ctx.FightLogRawData.Add(new FightLogRawData
        {
            FightLogId = fightLog.FightLogId,
            RawFightData = data.RawFightData,
            RawHealingData = data.RawHealingData,
            RawBarrierData = data.RawBarrierData
        });

        var gw2Players = playerService.GetGw2Players(data, fightPhase, sumAllTargets);
        var playerFightLogs = gw2Players.Select(p => BuildPlayerFightLog(p, fightLog.FightLogId)).ToList();
        ctx.PlayerFightLog.AddRange(playerFightLogs);
        await ctx.SaveChangesAsync(ct);

        var mechanicRecords = playerFightLogs
            .SelectMany(pfl =>
            {
                var player = gw2Players.FirstOrDefault(p => p.AccountName == pfl.GuildWarsAccountName);
                return player?.Mechanics.Select(m => new PlayerFightLogMechanic
                {
                    PlayerFightLogId = pfl.PlayerFightLogId,
                    MechanicName = m.Key,
                    MechanicCount = m.Value
                }) ?? [];
            }).ToList();

        if (mechanicRecords.Count > 0)
        {
            ctx.PlayerFightLogMechanic.AddRange(mechanicRecords);
            await ctx.SaveChangesAsync(ct);
        }

        return fightLog.FightLogId;
    }

    private static DateTime ParseStart(string? dateStartString) =>
        string.IsNullOrEmpty(dateStartString)
            ? DateTime.UtcNow
            : DateTime.ParseExact(dateStartString, "yyyy-MM-dd HH:mm:ss zzz", CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal);

    private static decimal D(double v, int decimals = 2) =>
        double.IsFinite(v) ? Math.Round((decimal)v, decimals) : 0m;

    private static PlayerFightLog BuildPlayerFightLog(Gw2Player p, long fightLogId) => new()
    {
        FightLogId = fightLogId,
        GuildWarsAccountName = p.AccountName,
        CharacterName = p.CharacterName,
        Damage = p.Damage,
        Cleave = p.Cleave,
        Kills = p.Kills,
        Downs = p.Downs,
        Deaths = p.Deaths,
        QuicknessDuration = D(p.TotalQuick),
        AlacDuration = D(p.TotalAlac),
        SubGroup = p.SubGroup,
        DamageDownContribution = p.DamageDownContribution,
        Cleanses = Convert.ToInt64(p.Cleanses),
        Strips = Convert.ToInt64(p.Strips),
        StabGenOnGroup = D(p.StabOnGroup),
        StabGenOffGroup = D(p.StabOffGroup),
        Healing = p.Healing,
        BarrierGenerated = p.BarrierGenerated,
        DistanceFromTag = D(p.DistanceFromTag),
        TimesDowned = Convert.ToInt32(p.TimesDowned),
        Interrupts = p.Interrupts,
        NumberOfHitsWhileBlinded = p.NumberOfHitsWhileBlinded,
        NumberOfMissesAgainst = Convert.ToInt64(p.NumberOfMissesAgainst),
        NumberOfTimesBlockedAttack = Convert.ToInt64(p.NumberOfTimesBlockedAttack),
        NumberOfTimesEnemyBlockedAttack = p.NumberOfTimesEnemyBlockedAttack,
        NumberOfBoonsRipped = Convert.ToInt64(p.NumberOfBoonsRipped),
        DamageTaken = Convert.ToInt64(p.DamageTaken),
        BarrierMitigation = Convert.ToInt64(p.BarrierMitigation),
        TimesInterrupted = p.TimesInterrupted,
        ResurrectionTime = p.ResurrectionTime,
        TimeOfDeath = p.TimeOfDeath
    };

    private static (short encounterType, bool sumAllTargets) GetPvEEncounterType(long fightId) => fightId switch
    {
        131329 => ((short)FightTypesEnum.Vale, false),
        131332 => ((short)FightTypesEnum.Spirit, true),
        131330 => ((short)FightTypesEnum.Gorseval, false),
        131331 => ((short)FightTypesEnum.Sabetha, false),
        131585 => ((short)FightTypesEnum.Sloth, false),
        131586 => ((short)FightTypesEnum.Trio, false),
        131587 => ((short)FightTypesEnum.Matthias, false),
        131841 => ((short)FightTypesEnum.Escort, false),
        131842 => ((short)FightTypesEnum.Kc, false),
        131843 => ((short)FightTypesEnum.Tc, false),
        131844 => ((short)FightTypesEnum.Xera, false),
        132097 => ((short)FightTypesEnum.Cairn, false),
        132098 => ((short)FightTypesEnum.Mo, false),
        132099 => ((short)FightTypesEnum.Samarog, false),
        132100 => ((short)FightTypesEnum.Deimos, false),
        132353 => ((short)FightTypesEnum.Sh, false),
        132354 => ((short)FightTypesEnum.River, false),
        132355 => ((short)FightTypesEnum.Bk, false),
        132356 => ((short)FightTypesEnum.EoS, false),
        132357 => ((short)FightTypesEnum.SoD, false),
        132358 => ((short)FightTypesEnum.Dhuum, false),
        132609 => ((short)FightTypesEnum.Ca, false),
        132610 => ((short)FightTypesEnum.Largos, true),
        132611 => ((short)FightTypesEnum.Qadim, false),
        132865 => ((short)FightTypesEnum.Adina, false),
        132866 => ((short)FightTypesEnum.Sabir, false),
        132867 => ((short)FightTypesEnum.Peerless, false),
        133121 => ((short)FightTypesEnum.Greer, false),
        133122 => ((short)FightTypesEnum.Decima, false),
        133123 => ((short)FightTypesEnum.Ura, false),
        262657 => ((short)FightTypesEnum.Icebrood, true),
        262658 => ((short)FightTypesEnum.Fraenir, true),
        262659 => ((short)FightTypesEnum.Kodan, true),
        262661 => ((short)FightTypesEnum.Whisper, true),
        262660 => ((short)FightTypesEnum.Boneskinner, true),
        262913 => ((short)FightTypesEnum.Ah, true),
        262914 => ((short)FightTypesEnum.Xjj, true),
        262915 => ((short)FightTypesEnum.Ko, true),
        262916 => ((short)FightTypesEnum.Ht, true),
        262917 => ((short)FightTypesEnum.Olc, true),
        263425 => ((short)FightTypesEnum.Co, true),
        263426 => ((short)FightTypesEnum.ToF, true),
        196865 => ((short)FightTypesEnum.Mama, true),
        196866 => ((short)FightTypesEnum.Siax, true),
        196867 => ((short)FightTypesEnum.Ensolyss, true),
        197121 => ((short)FightTypesEnum.Skorvald, true),
        197122 => ((short)FightTypesEnum.Artsariiv, true),
        197123 => ((short)FightTypesEnum.Arkk, true),
        197378 => ((short)FightTypesEnum.AiEle, true),
        197379 => ((short)FightTypesEnum.AiDark, true),
        197377 => ((short)FightTypesEnum.AiBoth, true),
        197633 => ((short)FightTypesEnum.Kanaxai, true),
        197890 => ((short)FightTypesEnum.Eparch, true),
        198145 => ((short)FightTypesEnum.Shadow, true),
        263681 => ((short)FightTypesEnum.Kela, true),
        _ => ((short)FightTypesEnum.Unkn, true)
    };

    private static string GetLogSource(string url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri) ? uri.Host : "upload";

    private static void Cleanup(string? evtcPath, string? jobOutputDir)
    {
        try
        {
            var evtcDir = evtcPath != null ? Path.GetDirectoryName(evtcPath) : null;
            if (evtcDir != null && Directory.Exists(evtcDir))
                Directory.Delete(evtcDir, recursive: true);
        }
        catch { /* best-effort */ }

        try
        {
            if (jobOutputDir != null && Directory.Exists(jobOutputDir))
                Directory.Delete(jobOutputDir, recursive: true);
        }
        catch { /* best-effort */ }
    }

    private sealed class DpsReportUploadResult
    {
        [JsonPropertyName("permalink")]
        public string? Permalink { get; set; }
    }
}
