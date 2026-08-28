using DonBot.Api.Services;
using DonBot.Core.Models.Entities;
using DonBot.Core.Models.GuildWars2;
using DonBot.Core.Services.GuildWars2;
using DonBot.Services.GuildWarsServices;
using DonBot.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace DonBot.Tests.Services.Api;

public class LogUploadPipelineServiceTests
{
    [Fact]
    public async Task ProcessUploadAsync_UrlUploadRetainsGuildIdWhenSavingFight()
    {
        using var db = new SqliteTestDb();
        long uploadId;
        await using (var context = await db.Factory.CreateDbContextAsync())
        {
            var upload = new LogUpload
            {
                DiscordId = 123,
                GuildId = 42,
                FileName = "abc-123",
                SourceType = "url",
                Status = "pending",
                DpsReportUrl = "https://dps.report/abc-123",
                SubmitToWingman = false
            };
            context.LogUpload.Add(upload);
            await context.SaveChangesAsync();
            uploadId = upload.LogUploadId;
        }

        var services = new ServiceCollection();
        var discordDelivery = new FakeDiscordUploadDeliveryService();
        var pointsAwardService = new StubPointsAwardService();
        var rotationAnalysisService = new StubRotationAnalysisService();
        services.AddSingleton<IDataModelGenerationService>(new StubDataModelGenerationService(BuildData()));
        services.AddSingleton<IPlayerService, StubPlayerService>();
        services.AddSingleton<IPointsAwardService>(pointsAwardService);
        services.AddSingleton<IRotationAnalysisService>(rotationAnalysisService);
        services.AddSingleton<IDiscordUploadDeliveryService>(discordDelivery);
        services.AddHttpClient();
        await using var provider = services.BuildServiceProvider();

        var pipeline = new LogUploadPipelineService(
            new LogUploadProgressService(),
            db.Factory,
            provider.GetRequiredService<IServiceScopeFactory>(),
            provider.GetRequiredService<IHttpClientFactory>(),
            new ConfigurationBuilder().Build(),
            NullLogger<LogUploadPipelineService>.Instance,
            new FightLogIngestionService(db.Factory));

        await pipeline.ProcessUploadAsync(uploadId, CancellationToken.None);

        await using var verificationContext = await db.Factory.CreateDbContextAsync();
        var fight = Assert.Single(await verificationContext.FightLog.ToListAsync());
        Assert.Equal(42, fight.GuildId);
        var completedUpload = Assert.Single(await verificationContext.LogUpload.ToListAsync());
        Assert.Equal("complete", completedUpload.Status);
        Assert.Equal(fight.FightLogId, completedUpload.FightLogId);
        Assert.Equal(new[] { uploadId }, discordDelivery.DeliveredUploadIds);
        Assert.Equal(1, pointsAwardService.CallCount);
        Assert.Equal(1, rotationAnalysisService.CallCount);
    }

    [Fact]
    public async Task ProcessUploadAsync_CheckpointRecoveryDoesNotRepeatIngestionScoringOrRotationAnalysis()
    {
        using var db = new SqliteTestDb();
        long uploadId;
        await using (var context = await db.Factory.CreateDbContextAsync())
        {
            var fight = new FightLog
            {
                GuildId = 42,
                FightType = 1,
                FightStart = DateTime.UtcNow,
                FightDurationInMs = 60_000,
                Url = "https://dps.report/abc-123"
            };
            context.FightLog.Add(fight);
            await context.SaveChangesAsync();
            var upload = new LogUpload
            {
                DiscordId = 123,
                GuildId = 42,
                FileName = "abc-123",
                SourceType = "url",
                Status = "delivering",
                DpsReportUrl = "https://dps.report/abc-123",
                FightLogId = fight.FightLogId,
                DiscordDeliveryMode = "guild_defaults"
            };
            context.LogUpload.Add(upload);
            await context.SaveChangesAsync();
            uploadId = upload.LogUploadId;
        }

        var pointsAwardService = new StubPointsAwardService();
        var rotationAnalysisService = new StubRotationAnalysisService();
        var delivery = new FakeDiscordUploadDeliveryService();
        var services = new ServiceCollection();
        services.AddSingleton<IDataModelGenerationService>(new StubDataModelGenerationService(BuildData()));
        services.AddSingleton<IPlayerService, StubPlayerService>();
        services.AddSingleton<IPointsAwardService>(pointsAwardService);
        services.AddSingleton<IRotationAnalysisService>(rotationAnalysisService);
        services.AddSingleton<IDiscordUploadDeliveryService>(delivery);
        services.AddHttpClient();
        await using var provider = services.BuildServiceProvider();
        var pipeline = CreatePipeline(db, provider);

        await pipeline.ProcessUploadAsync(uploadId, CancellationToken.None);

        await using var verification = await db.Factory.CreateDbContextAsync();
        Assert.Equal("complete", (await verification.LogUpload.SingleAsync()).Status);
        Assert.Empty(await verification.PlayerFightLog.ToListAsync());
        Assert.Equal(0, pointsAwardService.CallCount);
        Assert.Equal(0, rotationAnalysisService.CallCount);
        Assert.Equal(new[] { uploadId }, delivery.DeliveredUploadIds);
    }

    [Fact]
    public async Task ProcessUploadAsync_CheckpointRecoveryFailureRecordsTerminalDeliveryFailure()
    {
        using var db = new SqliteTestDb();
        long uploadId;
        await using (var context = await db.Factory.CreateDbContextAsync())
        {
            var fight = new FightLog
            {
                GuildId = 42,
                FightType = 1,
                FightStart = DateTime.UtcNow,
                FightDurationInMs = 60_000,
                Url = "https://dps.report/abc-123"
            };
            context.FightLog.Add(fight);
            await context.SaveChangesAsync();
            var upload = new LogUpload
            {
                DiscordId = 123,
                GuildId = 42,
                FileName = "abc-123",
                SourceType = "url",
                Status = "delivering",
                DpsReportUrl = "https://dps.report/abc-123",
                FightLogId = fight.FightLogId,
                DiscordDeliveryMode = "guild_defaults"
            };
            context.LogUpload.Add(upload);
            await context.SaveChangesAsync();
            uploadId = upload.LogUploadId;
        }

        var delivery = new FakeDiscordUploadDeliveryService
        {
            Result = new DiscordDeliveryResult(true, "failed", 0, 0, 1, 0)
        };
        var services = new ServiceCollection();
        services.AddSingleton<IDataModelGenerationService>(new ThrowingDataModelGenerationService());
        services.AddSingleton<IPlayerService, StubPlayerService>();
        services.AddSingleton<IPointsAwardService, StubPointsAwardService>();
        services.AddSingleton<IRotationAnalysisService, StubRotationAnalysisService>();
        services.AddSingleton<IDiscordUploadDeliveryService>(delivery);
        services.AddHttpClient();
        await using var provider = services.BuildServiceProvider();
        var pipeline = CreatePipeline(db, provider);

        await pipeline.ProcessUploadAsync(uploadId, CancellationToken.None);

        await using var verification = await db.Factory.CreateDbContextAsync();
        Assert.Equal("complete", (await verification.LogUpload.SingleAsync()).Status);
        Assert.Equal(new[] { uploadId }, delivery.FailedUploadIds);
        Assert.Empty(delivery.DeliveredUploadIds);
    }

    [Fact]
    public async Task RecoverInterruptedUploadsAsync_QueuesUploadingState()
    {
        using var db = new SqliteTestDb();
        long uploadId;
        await using (var context = await db.Factory.CreateDbContextAsync())
        {
            var upload = new LogUpload
            {
                DiscordId = 123,
                FileName = "fight.zevtc",
                SourceType = "file",
                Status = "uploading"
            };
            context.LogUpload.Add(upload);
            await context.SaveChangesAsync();
            uploadId = upload.LogUploadId;
        }

        var services = new ServiceCollection();
        services.AddSingleton<IDiscordUploadDeliveryService, FakeDiscordUploadDeliveryService>();
        services.AddHttpClient();
        await using var provider = services.BuildServiceProvider();
        var pipeline = CreatePipeline(db, provider);

        await pipeline.RecoverInterruptedUploadsAsync(CancellationToken.None);

        Assert.True(pipeline.TryReadQueuedUpload(out var queuedId));
        Assert.Equal(uploadId, queuedId);
    }

    [Fact]
    public async Task ProcessUploadAsync_CancellationBeforeCheckpointPreservesSourceFile()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        using var db = new SqliteTestDb();
        using var cts = new CancellationTokenSource();
        var testRoot = Path.Combine(Path.GetTempPath(), $"donbot-cancel-{Guid.NewGuid():N}");
        var storagePath = Path.Combine(testRoot, "uploads");
        var outputPath = Path.Combine(testRoot, "output");
        var parserPath = Path.Combine(testRoot, "fake-ei.exe");
        Directory.CreateDirectory(testRoot);
        await File.WriteAllTextAsync(
            parserPath,
            "#!/bin/sh\nprintf '%s\\n' 'https://dps.report/abc-123'\n");
        File.SetUnixFileMode(
            parserPath,
            UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);

        try
        {
            long uploadId;
            await using (var context = await db.Factory.CreateDbContextAsync())
            {
                var upload = new LogUpload
                {
                    DiscordId = 123,
                    FileName = "fight.zevtc",
                    SourceType = "file",
                    Status = "stored"
                };
                context.LogUpload.Add(upload);
                await context.SaveChangesAsync();
                uploadId = upload.LogUploadId;
            }

            var sourceDirectory = Path.Combine(storagePath, uploadId.ToString());
            var sourcePath = Path.Combine(sourceDirectory, "fight.zevtc");
            Directory.CreateDirectory(sourceDirectory);
            await File.WriteAllBytesAsync(sourcePath, [1, 2, 3]);

            var services = new ServiceCollection();
            services.AddSingleton<IDataModelGenerationService>(new CancelingDataModelGenerationService(cts));
            services.AddSingleton<IPlayerService, StubPlayerService>();
            services.AddSingleton<IPointsAwardService, StubPointsAwardService>();
            services.AddSingleton<IRotationAnalysisService, StubRotationAnalysisService>();
            services.AddSingleton<IDiscordUploadDeliveryService, FakeDiscordUploadDeliveryService>();
            services.AddHttpClient();
            await using var provider = services.BuildServiceProvider();
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Upload:StoragePath"] = storagePath,
                    ["EliteInsights:DllPath"] = parserPath,
                    ["EliteInsights:OutputBasePath"] = outputPath
                })
                .Build();
            var pipeline = CreatePipeline(db, provider, configuration);

            await pipeline.ProcessUploadAsync(uploadId, cts.Token);

            Assert.True(File.Exists(sourcePath));
            await using var verification = await db.Factory.CreateDbContextAsync();
            Assert.Equal("saving", (await verification.LogUpload.SingleAsync()).Status);
        }
        finally
        {
            if (Directory.Exists(testRoot))
            {
                Directory.Delete(testRoot, recursive: true);
            }
        }
    }

    private static LogUploadPipelineService CreatePipeline(
        SqliteTestDb db,
        ServiceProvider provider,
        IConfiguration? configuration = null) =>
        new(
            new LogUploadProgressService(),
            db.Factory,
            provider.GetRequiredService<IServiceScopeFactory>(),
            provider.GetRequiredService<IHttpClientFactory>(),
            configuration ?? new ConfigurationBuilder().Build(),
            NullLogger<LogUploadPipelineService>.Instance,
            new FightLogIngestionService(db.Factory));

    private static EliteInsightDataModel BuildData() =>
        new(
            new FightEliteInsightDataModel
            {
                Url = "https://dps.report/abc-123",
                EncounterId = 131332,
                EncounterStart = "2026-06-29 10:00:00 +00:00",
                Success = true,
                Targets = [new ArcDpsTarget { Percent = 50f, Health = 100 }],
                Phases =
                [
                    new ArcDpsPhase
                    {
                        Duration = 60_000,
                        Success = true,
                        EncounterDuration = "00:01:00.000"
                    }
                ]
            },
            new HealingEliteInsightDataModel(),
            new BarrierEliteInsightDataModel(),
            "raw-fight-data",
            null,
            null);

    private sealed class StubDataModelGenerationService(EliteInsightDataModel data) : IDataModelGenerationService
    {
        public Task<EliteInsightDataModel> GenerateEliteInsightDataModelFromUrl(string url) => Task.FromResult(data);

        public EliteInsightDataModel GenerateEliteInsightDataModelFromHtml(string html, string url) => data;

        public EliteInsightDataModel GenerateEliteInsightDataModelFromJson(string json, string url) => data;
    }

    private sealed class ThrowingDataModelGenerationService : IDataModelGenerationService
    {
        public Task<EliteInsightDataModel> GenerateEliteInsightDataModelFromUrl(string url) =>
            throw new HttpRequestException("dps.report unavailable");

        public EliteInsightDataModel GenerateEliteInsightDataModelFromHtml(string html, string url) =>
            throw new NotImplementedException();

        public EliteInsightDataModel GenerateEliteInsightDataModelFromJson(string json, string url) =>
            throw new NotImplementedException();
    }

    private sealed class CancelingDataModelGenerationService(CancellationTokenSource cts) : IDataModelGenerationService
    {
        public Task<EliteInsightDataModel> GenerateEliteInsightDataModelFromUrl(string url)
        {
            cts.Cancel();
            return Task.FromCanceled<EliteInsightDataModel>(cts.Token);
        }

        public EliteInsightDataModel GenerateEliteInsightDataModelFromHtml(string html, string url) =>
            throw new NotImplementedException();

        public EliteInsightDataModel GenerateEliteInsightDataModelFromJson(string json, string url) =>
            throw new NotImplementedException();
    }

    private sealed class StubPlayerService : IPlayerService
    {
        public Task SetPlayerPoints(EliteInsightDataModel eliteInsightDataModel) => Task.CompletedTask;

        public List<Gw2Player> GetGw2Players(EliteInsightDataModel data, ArcDpsPhase fightPhase, bool someAllFights = true) => [];
    }

    private sealed class StubPointsAwardService : IPointsAwardService
    {
        public int CallCount { get; private set; }

        public Task<IReadOnlyList<PlayerPointAward>> AwardFightAsync(long fightLogId, CancellationToken ct = default)
        {
            CallCount++;
            return Task.FromResult<IReadOnlyList<PlayerPointAward>>([]);
        }
    }

    private sealed class StubRotationAnalysisService : IRotationAnalysisService
    {
        public int CallCount { get; private set; }

        public Task AnalyzePlayerRotations(EliteInsightDataModel data)
        {
            CallCount++;
            return Task.CompletedTask;
        }
    }
}
