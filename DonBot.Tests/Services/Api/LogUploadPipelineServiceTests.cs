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
        services.AddSingleton<IDataModelGenerationService>(new StubDataModelGenerationService(BuildData()));
        services.AddSingleton<IPlayerService, StubPlayerService>();
        services.AddSingleton<IPointsAwardService, StubPointsAwardService>();
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
    }

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

    private sealed class StubPlayerService : IPlayerService
    {
        public Task SetPlayerPoints(EliteInsightDataModel eliteInsightDataModel) => Task.CompletedTask;

        public List<Gw2Player> GetGw2Players(EliteInsightDataModel data, ArcDpsPhase fightPhase, bool someAllFights = true) => [];
    }

    private sealed class StubPointsAwardService : IPointsAwardService
    {
        public Task<IReadOnlyList<PlayerPointAward>> AwardFightAsync(long fightLogId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<PlayerPointAward>>([]);
    }
}
