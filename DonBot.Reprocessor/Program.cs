using System.Diagnostics;
using System.Globalization;
using System.Text;
using DonBot.Configuration;
using DonBot.Core.Models.Entities;
using DonBot.Core.Models.Enums;
using DonBot.Core.Models.GuildWars2;
using DonBot.Core.Services;
using DonBot.Services.GuildWarsServices;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Newtonsoft.Json;
using Npgsql;

namespace DonBot.Reprocessor;

public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        if (ReprocessorOptions.TryParse(args, out var options, out var error) is false)
        {
            Console.Error.WriteLine(error);
            PrintHelp();
            return 1;
        }

        if (options.ShowHelp)
        {
            PrintHelp();
            return 0;
        }

        if (options.HasWork is false)
        {
            Console.Error.WriteLine("No reprocessor task selected.");
            PrintHelp();
            return 1;
        }

        RuntimeConfiguration.LoadEnvFile();

        var configuration = new ConfigurationBuilder()
            .AddRuntimeConfiguration(args, reloadOnChange: false)
            .Build();

        var connectionString = configuration["DonBotSqlConnectionString"];
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            Console.Error.WriteLine("DonBotSqlConnectionString is missing. Set it in .env, appsettings.user.json, or the environment.");
            return 1;
        }

        var dbOptions = new DbContextOptionsBuilder<DatabaseContext>()
            .UseNpgsql(connectionString, npgsql => npgsql.MigrationsAssembly("DonBot"))
            .Options;

        var factory = new DatabaseContextFactory(dbOptions);
        using var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (_, eventArgs) =>
        {
            eventArgs.Cancel = true;
            cts.Cancel();
            Console.WriteLine("Cancellation requested; finishing the current operation.");
        };

        try
        {
            if (options.BackfillPlaytypes)
            {
                await PlaytypeBackfillRunner.RunAsync(factory, options, cts.Token);
            }

            if (options.AwardMissingPoints)
            {
                await MissingPointsRunner.RunAsync(factory, options, cts.Token);
            }

            if (options.BackfillUraProgress)
            {
                await UraProgressBackfillRunner.RunAsync(factory, options, cts.Token);
            }

            if (options.BackfillHtProgress)
            {
                await HarvestTempleProgressBackfillRunner.RunAsync(factory, options, cts.Token);
            }

            return 0;
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine("Reprocessor cancelled.");
            return 2;
        }
    }

    private static void PrintHelp()
    {
        Console.WriteLine("""
        DonBot.Reprocessor

        Usage:
          dotnet run --project DonBot.Reprocessor -- --backfill-playtypes
          dotnet run --project DonBot.Reprocessor -- --award-missing-points
          dotnet run --project DonBot.Reprocessor -- --backfill-ura-progress
          dotnet run --project DonBot.Reprocessor -- --backfill-ht-progress
          dotnet run --project DonBot.Reprocessor -- --all

        Tasks:
          --backfill-playtypes       Rebuild stab, quick/alac generation, boon role, and playstyle from raw log JSON.
          --award-missing-points     Award missing dynamic point rows for existing fights.
          --backfill-ura-progress    Rebuild Ura CM/LCM fight percent and phase from raw log JSON.
          --backfill-ht-progress     Rebuild Harvest Temple fight percent and phase from raw log JSON.
          --all                      Run all currently registered reprocessors.

        Options:
          --batch-size <number>      Fight logs to process per batch. Default: 100.
          --from-id <number>         Only process fight logs with this id or higher.
          --to-id <number>           Only process fight logs with this id or lower.
          --force                    Reprocess stored playtypes or recheck all eligible fights for missing point components.
          --dry-run                  Read and calculate only; do not write database updates.
          --help                     Show this help.
        """);
    }
}

public sealed record ReprocessorOptions(
    bool BackfillPlaytypes,
    bool AwardMissingPoints,
    bool BackfillUraProgress,
    bool BackfillHtProgress,
    bool DryRun,
    bool Force,
    bool ShowHelp,
    int BatchSize,
    long? FromId,
    long? ToId)
{
    public bool HasWork => BackfillPlaytypes || AwardMissingPoints || BackfillUraProgress || BackfillHtProgress;

    public static bool TryParse(string[] args, out ReprocessorOptions options, out string? error)
    {
        var backfillPlaytypes = false;
        var awardMissingPoints = false;
        var backfillUraProgress = false;
        var backfillHtProgress = false;
        var dryRun = false;
        var force = false;
        var showHelp = false;
        var batchSize = 100;
        long? fromId = null;
        long? toId = null;

        for (var index = 0; index < args.Length; index++)
        {
            var arg = args[index];
            switch (arg)
            {
                case "--backfill-playtypes":
                case "--backfill-playstyles":
                    backfillPlaytypes = true;
                    break;
                case "--award-missing-points":
                    awardMissingPoints = true;
                    break;
                case "--backfill-ura-progress":
                case "--update-ura-progress":
                    backfillUraProgress = true;
                    break;
                case "--backfill-ht-progress":
                case "--update-ht-progress":
                    backfillHtProgress = true;
                    break;
                case "--all":
                    backfillPlaytypes = true;
                    awardMissingPoints = true;
                    backfillUraProgress = true;
                    backfillHtProgress = true;
                    break;
                case "--dry-run":
                    dryRun = true;
                    break;
                case "--force":
                    force = true;
                    break;
                case "--help":
                case "-h":
                    showHelp = true;
                    break;
                case "--batch-size":
                    if (TryReadInt(args, ref index, arg, out batchSize, out error) is false)
                    {
                        options = Empty;
                        return false;
                    }

                    if (batchSize <= 0)
                    {
                        options = Empty;
                        error = "--batch-size must be greater than 0.";
                        return false;
                    }

                    break;
                case "--from-id":
                    if (TryReadLong(args, ref index, arg, out var parsedFromId, out error) is false)
                    {
                        options = Empty;
                        return false;
                    }

                    fromId = parsedFromId;
                    break;
                case "--to-id":
                    if (TryReadLong(args, ref index, arg, out var parsedToId, out error) is false)
                    {
                        options = Empty;
                        return false;
                    }

                    toId = parsedToId;
                    break;
                default:
                    options = Empty;
                    error = $"Unknown argument '{arg}'.";
                    return false;
            }
        }

        if (fromId.HasValue && toId.HasValue && fromId.Value > toId.Value)
        {
            options = Empty;
            error = "--from-id cannot be greater than --to-id.";
            return false;
        }

        options = new ReprocessorOptions(backfillPlaytypes, awardMissingPoints, backfillUraProgress, backfillHtProgress, dryRun, force, showHelp, batchSize, fromId, toId);
        error = null;
        return true;
    }

    private static ReprocessorOptions Empty => new(false, false, false, false, false, false, false, 100, null, null);

    private static bool TryReadInt(string[] args, ref int index, string name, out int value, out string? error)
    {
        if (TryReadValue(args, ref index, name, out var rawValue, out error) is false)
        {
            value = 0;
            return false;
        }

        if (int.TryParse(rawValue, NumberStyles.None, CultureInfo.InvariantCulture, out value))
        {
            return true;
        }

        error = $"{name} expects a whole number.";
        return false;
    }

    private static bool TryReadLong(string[] args, ref int index, string name, out long value, out string? error)
    {
        if (TryReadValue(args, ref index, name, out var rawValue, out error) is false)
        {
            value = 0;
            return false;
        }

        if (long.TryParse(rawValue, NumberStyles.None, CultureInfo.InvariantCulture, out value))
        {
            return true;
        }

        error = $"{name} expects a whole number.";
        return false;
    }

    private static bool TryReadValue(string[] args, ref int index, string name, out string value, out string? error)
    {
        if (index + 1 >= args.Length)
        {
            value = string.Empty;
            error = $"{name} expects a value.";
            return false;
        }

        value = args[++index];
        if (value.StartsWith("--", StringComparison.Ordinal))
        {
            error = $"{name} expects a value.";
            return false;
        }

        error = null;
        return true;
    }
}

internal static class PlaytypeBackfillRunner
{
    public static async Task RunAsync(IDbContextFactory<DatabaseContext> factory, ReprocessorOptions options, CancellationToken cancellationToken)
    {
        Console.WriteLine(options.DryRun
            ? "Starting playtype backfill dry run."
            : "Starting playtype backfill.");

        var stopwatch = Stopwatch.StartNew();
        var processedLogs = 0;
        var updatedPlayers = 0;
        var skippedPlayers = 0;
        var failedLogs = 0;

        await using var countContext = await factory.CreateDbContextAsync(cancellationToken);
        var totalLogs = await BuildBackfillQuery(countContext, options, null)
            .CountAsync(cancellationToken);

        Console.WriteLine($"Queued {totalLogs:N0} fight logs for playtype backfill.");

        var playerService = new PlayerService(null!);
        long? lastFightLogId = null;

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            List<FightRawBatchRow> fightRows;
            await using (var queryContext = await factory.CreateDbContextAsync(cancellationToken))
            {
                fightRows = await BuildBackfillQuery(queryContext, options, lastFightLogId)
                    .Take(options.BatchSize)
                    .ToListAsync(cancellationToken);
            }

            if (fightRows.Count == 0)
            {
                break;
            }

            lastFightLogId = fightRows[^1].FightLogId;
            var fightIds = fightRows.Select(row => row.FightLogId).ToArray();

            List<PlayerLogLite> playerLogs;
            await using (var playerContext = await factory.CreateDbContextAsync(cancellationToken))
            {
                playerLogs = await playerContext.PlayerFightLog
                    .Where(playerLog => fightIds.Contains(playerLog.FightLogId))
                    .Select(playerLog => new PlayerLogLite(
                        playerLog.PlayerFightLogId,
                        playerLog.FightLogId,
                        playerLog.GuildWarsAccountName))
                    .ToListAsync(cancellationToken);
            }

            var playerLogsByFight = playerLogs
                .GroupBy(playerLog => playerLog.FightLogId)
                .ToDictionary(group => group.Key, group => group.ToArray());

            var updates = new List<PlayerLogUpdate>(playerLogs.Count);

            foreach (var fightRow in fightRows)
            {
                try
                {
                    var fightPlayerLogs = playerLogsByFight.GetValueOrDefault(fightRow.FightLogId);
                    if (fightPlayerLogs is null || fightPlayerLogs.Length == 0)
                    {
                        processedLogs++;
                        continue;
                    }

                    var dataModel = DeserializeFightData(fightRow);
                    var fightPhase = dataModel.FightEliteInsightDataModel.Phases?.FirstOrDefault() ?? new ArcDpsPhase();
                    var shouldSumAllTargets = fightRow.FightType == (short)FightTypesEnum.WvW || ShouldSumAllTargets(fightRow.FightType);
                    var gw2Players = playerService.GetGw2Players(dataModel, fightPhase, shouldSumAllTargets);

                    if (gw2Players.Count == 0)
                    {
                        skippedPlayers += fightPlayerLogs.Length;
                        processedLogs++;
                        continue;
                    }

                    var averageGroupDps = PlayerFightLogRoleClassifier.GetAverageGroupDps(gw2Players, fightRow.FightDuration);
                    var wvwBenchmarks = fightRow.FightType == (short)FightTypesEnum.WvW
                        ? PlayerFightLogPlaystyleClassifier.BuildWvwBenchmarks(gw2Players, fightRow.FightDuration)
                        : null;
                    var gw2PlayersByAccount = gw2Players
                        .GroupBy(player => player.AccountName, StringComparer.OrdinalIgnoreCase)
                        .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

                    foreach (var playerLog in fightPlayerLogs)
                    {
                        if (gw2PlayersByAccount.TryGetValue(playerLog.GuildWarsAccountName, out var gw2Player) is false)
                        {
                            skippedPlayers++;
                            continue;
                        }

                        var boonRole = PlayerFightLogRoleClassifier.ResolveBoonRole(gw2Player, fightRow.FightDuration, averageGroupDps);
                        var playstyle = fightRow.FightType == (short)FightTypesEnum.WvW
                            ? PlayerFightLogPlaystyleClassifier.ResolveWvwPlaystyle(gw2Player, fightRow.FightDuration, wvwBenchmarks!)
                            : PlayerFightLogPlaystyleClassifier.ResolvePvePlaystyle(gw2Player, fightRow.FightDuration, averageGroupDps);

                        updates.Add(new PlayerLogUpdate(
                            playerLog.PlayerFightLogId,
                            ToDatabaseDecimal(gw2Player.StabOnGroup),
                            ToDatabaseDecimal(gw2Player.StabOffGroup),
                            ToDatabaseDecimal(gw2Player.QuicknessGenGroup),
                            ToDatabaseDecimal(gw2Player.AlacGenGroup),
                            boonRole,
                            playstyle));
                    }

                    processedLogs++;
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    failedLogs++;
                    Console.Error.WriteLine($"Failed to process FightLogId {fightRow.FightLogId}: {ex.Message}");
                    processedLogs++;
                }
            }

            if (updates.Count > 0 && options.DryRun is false)
            {
                await using var updateContext = await factory.CreateDbContextAsync(cancellationToken);
                updateContext.ChangeTracker.AutoDetectChangesEnabled = false;
                await BulkUpdatePlayerLogsAsync(updateContext, updates, cancellationToken);
            }

            updatedPlayers += updates.Count;
            ProgressWriter.Write("Playtypes", processedLogs, totalLogs, stopwatch, $"player updates {updatedPlayers:N0}, skipped {skippedPlayers:N0}, errors {failedLogs:N0}");
        }

        Console.WriteLine($"Playtype backfill complete. Logs {processedLogs:N0}/{totalLogs:N0}, player updates {updatedPlayers:N0}, skipped {skippedPlayers:N0}, errors {failedLogs:N0}.");
    }

    private static IQueryable<FightRawBatchRow> BuildBackfillQuery(DatabaseContext context, ReprocessorOptions options, long? afterFightLogId)
    {
        var query =
            from fightLog in context.FightLog
            join rawData in context.FightLogRawData on fightLog.FightLogId equals rawData.FightLogId
            where rawData.RawFightData != null && rawData.RawFightData != string.Empty
            select new
            {
                fightLog.FightLogId,
                fightLog.FightType,
                fightLog.FightDurationInMs,
                rawData.RawFightData,
                rawData.RawHealingData,
                rawData.RawBarrierData
            };

        if (afterFightLogId.HasValue)
        {
            query = query.Where(row => row.FightLogId > afterFightLogId.Value);
        }

        if (options.FromId.HasValue)
        {
            query = query.Where(row => row.FightLogId >= options.FromId.Value);
        }

        if (options.ToId.HasValue)
        {
            query = query.Where(row => row.FightLogId <= options.ToId.Value);
        }

        if (options.Force is false)
        {
            query = query.Where(row => context.PlayerFightLog.Any(playerLog =>
                playerLog.FightLogId == row.FightLogId &&
                (playerLog.Playstyle == null || playerLog.Playstyle == string.Empty)));
        }

        return query
            .OrderBy(row => row.FightLogId)
            .Select(row => new FightRawBatchRow(
                row.FightLogId,
                row.FightType,
                row.FightDurationInMs,
                row.RawFightData!,
                row.RawHealingData,
                row.RawBarrierData));
    }

    private static EliteInsightDataModel DeserializeFightData(FightRawBatchRow row)
    {
        var fightData = JsonConvert.DeserializeObject<FightEliteInsightDataModel>(row.RawFightData) ?? new FightEliteInsightDataModel();
        var healingData = string.IsNullOrWhiteSpace(row.RawHealingData)
            ? new HealingEliteInsightDataModel()
            : JsonConvert.DeserializeObject<HealingEliteInsightDataModel>(row.RawHealingData) ?? new HealingEliteInsightDataModel();
        var barrierData = string.IsNullOrWhiteSpace(row.RawBarrierData)
            ? new BarrierEliteInsightDataModel()
            : JsonConvert.DeserializeObject<BarrierEliteInsightDataModel>(row.RawBarrierData) ?? new BarrierEliteInsightDataModel();

        return new EliteInsightDataModel(
            fightData,
            healingData,
            barrierData,
            row.RawFightData,
            row.RawHealingData,
            row.RawBarrierData);
    }

    private static async Task BulkUpdatePlayerLogsAsync(DatabaseContext context, IReadOnlyList<PlayerLogUpdate> updates, CancellationToken cancellationToken)
    {
        var values = new StringBuilder(updates.Count * 48);
        var parameters = new List<object>(updates.Count * 7);

        for (var index = 0; index < updates.Count; index++)
        {
            if (index > 0)
            {
                values.Append(", ");
            }

            values.Append(CultureInfo.InvariantCulture, $"(@id{index}, @stabOn{index}, @stabOff{index}, @quick{index}, @alac{index}, @boon{index}, @playstyle{index})");

            var update = updates[index];
            parameters.Add(new NpgsqlParameter($"id{index}", update.PlayerFightLogId));
            parameters.Add(new NpgsqlParameter($"stabOn{index}", update.StabGenOnGroup));
            parameters.Add(new NpgsqlParameter($"stabOff{index}", update.StabGenOffGroup));
            parameters.Add(new NpgsqlParameter($"quick{index}", update.QuicknessGenGroup));
            parameters.Add(new NpgsqlParameter($"alac{index}", update.AlacGenGroup));
            parameters.Add(new NpgsqlParameter($"boon{index}", update.BoonRole));
            parameters.Add(new NpgsqlParameter($"playstyle{index}", update.Playstyle));
        }

        var sql = $$"""
        UPDATE "PlayerFightLog" AS player_log
        SET "StabGenOnGroup" = updates."StabGenOnGroup",
            "StabGenOffGroup" = updates."StabGenOffGroup",
            "QuicknessGenGroup" = updates."QuicknessGenGroup",
            "AlacGenGroup" = updates."AlacGenGroup",
            "BoonRole" = updates."BoonRole",
            "Playstyle" = updates."Playstyle"
        FROM (VALUES {{values}}) AS updates("PlayerFightLogId", "StabGenOnGroup", "StabGenOffGroup", "QuicknessGenGroup", "AlacGenGroup", "BoonRole", "Playstyle")
        WHERE player_log."PlayerFightLogId" = updates."PlayerFightLogId";
        """;

        await context.Database.ExecuteSqlRawAsync(sql, parameters, cancellationToken);
    }

    private static decimal ToDatabaseDecimal(double value)
    {
        if (double.IsFinite(value) is false)
        {
            return 0m;
        }

        return Convert.ToDecimal(value);
    }

    private static bool ShouldSumAllTargets(short fightType)
    {
        return (FightTypesEnum)fightType switch
        {
            FightTypesEnum.Spirit => true,
            FightTypesEnum.Largos => true,
            FightTypesEnum.Icebrood => true,
            FightTypesEnum.Fraenir => true,
            FightTypesEnum.Kodan => true,
            FightTypesEnum.Whisper => true,
            FightTypesEnum.Boneskinner => true,
            FightTypesEnum.Ah => true,
            FightTypesEnum.Xjj => true,
            FightTypesEnum.Ko => true,
            FightTypesEnum.Ht => true,
            FightTypesEnum.Olc => true,
            FightTypesEnum.Co => true,
            FightTypesEnum.ToF => true,
            FightTypesEnum.Mama => true,
            FightTypesEnum.Siax => true,
            FightTypesEnum.Ensolyss => true,
            FightTypesEnum.Skorvald => true,
            FightTypesEnum.Artsariiv => true,
            FightTypesEnum.Arkk => true,
            FightTypesEnum.AiEle => true,
            FightTypesEnum.AiDark => true,
            FightTypesEnum.AiBoth => true,
            FightTypesEnum.Kanaxai => true,
            FightTypesEnum.Eparch => true,
            FightTypesEnum.Shadow => true,
            FightTypesEnum.Kela => true,
            _ => false
        };
    }
}

internal static class MissingPointsRunner
{
    public static async Task RunAsync(IDbContextFactory<DatabaseContext> factory, ReprocessorOptions options, CancellationToken cancellationToken)
    {
        Console.WriteLine(options.DryRun
            ? "Starting missing points dry run."
            : "Starting missing points reprocessor.");

        var stopwatch = Stopwatch.StartNew();
        var processedLogs = 0;
        var awardedRows = 0;
        var awardedPoints = 0m;
        var failedLogs = 0;

        await using var countContext = await factory.CreateDbContextAsync(cancellationToken);
        var totalLogs = await BuildAwardQuery(countContext, options, null)
            .CountAsync(cancellationToken);

        Console.WriteLine($"Queued {totalLogs:N0} fight logs for missing point awards.");

        long? lastFightLogId = null;
        var pointsAwardService = new PointsAwardService(factory, NullLogger<PointsAwardService>.Instance);

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            List<long> fightLogIds;
            await using (var queryContext = await factory.CreateDbContextAsync(cancellationToken))
            {
                fightLogIds = await BuildAwardQuery(queryContext, options, lastFightLogId)
                    .Take(options.BatchSize)
                    .ToListAsync(cancellationToken);
            }

            if (fightLogIds.Count == 0)
            {
                break;
            }

            lastFightLogId = fightLogIds[^1];

            foreach (var fightLogId in fightLogIds)
            {
                try
                {
                    if (options.DryRun)
                    {
                        processedLogs++;
                        continue;
                    }

                    var awards = await pointsAwardService.AwardFightAsync(fightLogId);
                    awardedRows += awards.Count;
                    awardedPoints += awards.Sum(award => award.Points);
                    processedLogs++;
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    failedLogs++;
                    Console.Error.WriteLine($"Failed to award points for FightLogId {fightLogId}: {ex.Message}");
                    processedLogs++;
                }
            }

            ProgressWriter.Write("Points", processedLogs, totalLogs, stopwatch, $"award rows {awardedRows:N0}, points {awardedPoints:N2}, errors {failedLogs:N0}");
        }

        Console.WriteLine($"Missing points reprocessor complete. Logs {processedLogs:N0}/{totalLogs:N0}, award rows {awardedRows:N0}, points {awardedPoints:N2}, errors {failedLogs:N0}.");
    }

    private static IQueryable<long> BuildAwardQuery(DatabaseContext context, ReprocessorOptions options, long? afterFightLogId)
    {
        var query = context.FightLog
            .Where(fightLog => fightLog.FightType != (short)FightTypesEnum.Unkn)
            .Where(fightLog => fightLog.FightType != (short)FightTypesEnum.Golem);

        if (options.Force is false)
        {
            query = query.Where(fightLog => context.PlayerFightLog.Any(playerLog =>
                playerLog.FightLogId == fightLog.FightLogId &&
                context.PlayerPointAward.Any(award => award.PlayerFightLogId == playerLog.PlayerFightLogId) == false));
        }

        if (afterFightLogId.HasValue)
        {
            query = query.Where(fightLog => fightLog.FightLogId > afterFightLogId.Value);
        }

        if (options.FromId.HasValue)
        {
            query = query.Where(fightLog => fightLog.FightLogId >= options.FromId.Value);
        }

        if (options.ToId.HasValue)
        {
            query = query.Where(fightLog => fightLog.FightLogId <= options.ToId.Value);
        }

        return query
            .OrderBy(fightLog => fightLog.FightLogId)
            .Select(fightLog => fightLog.FightLogId);
    }
}

internal static class UraProgressBackfillRunner
{
    public static async Task RunAsync(IDbContextFactory<DatabaseContext> factory, ReprocessorOptions options, CancellationToken cancellationToken)
    {
        Console.WriteLine(options.DryRun
            ? "Starting Ura CM/LCM progress dry run."
            : "Starting Ura CM/LCM progress backfill.");

        var stopwatch = Stopwatch.StartNew();
        var processedLogs = 0;
        var updatedLogs = 0;
        var unchangedLogs = 0;
        var skippedLogs = 0;
        var failedLogs = 0;

        await using var countContext = await factory.CreateDbContextAsync(cancellationToken);
        var totalLogs = await BuildUraProgressQuery(countContext, options, null)
            .CountAsync(cancellationToken);

        Console.WriteLine($"Queued {totalLogs:N0} Ura fight logs with raw data for progress backfill.");

        long? lastFightLogId = null;

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            List<UraProgressRow> fightRows;
            await using (var queryContext = await factory.CreateDbContextAsync(cancellationToken))
            {
                fightRows = await BuildUraProgressQuery(queryContext, options, lastFightLogId)
                    .Take(options.BatchSize)
                    .ToListAsync(cancellationToken);
            }

            if (fightRows.Count == 0)
            {
                break;
            }

            lastFightLogId = fightRows[^1].FightLogId;
            var updates = new List<UraProgressUpdate>(fightRows.Count);

            foreach (var fightRow in fightRows)
            {
                try
                {
                    var fightData = JsonConvert.DeserializeObject<FightEliteInsightDataModel>(fightRow.RawFightData) ?? new FightEliteInsightDataModel();
                    if (fightData.Targets is not { Count: > 0 })
                    {
                        skippedLogs++;
                        processedLogs++;
                        continue;
                    }

                    var rawFightMode = FightLogProgressCalculator.ResolveFightMode(fightData, fightData.Phases?.FirstOrDefault());
                    var fightMode = rawFightMode != 0 ? rawFightMode : fightRow.FightMode;
                    if (fightMode is not (1 or 2))
                    {
                        skippedLogs++;
                        processedLogs++;
                        continue;
                    }

                    var progress = FightLogProgressCalculator.Calculate(fightData, (short)FightTypesEnum.Ura, fightMode);
                    if (fightRow.FightMode == fightMode &&
                        fightRow.FightPhase == progress.FightPhase &&
                        fightRow.FightPercent == progress.FightPercent)
                    {
                        unchangedLogs++;
                        processedLogs++;
                        continue;
                    }

                    updates.Add(new UraProgressUpdate(
                        fightRow.FightLogId,
                        fightMode,
                        progress.FightPhase,
                        progress.FightPercent));
                    processedLogs++;
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    failedLogs++;
                    Console.Error.WriteLine($"Failed to update Ura progress for FightLogId {fightRow.FightLogId}: {ex.Message}");
                    processedLogs++;
                }
            }

            if (updates.Count > 0 && options.DryRun is false)
            {
                await using var updateContext = await factory.CreateDbContextAsync(cancellationToken);
                updateContext.ChangeTracker.AutoDetectChangesEnabled = false;
                await BulkUpdateUraProgressAsync(updateContext, updates, cancellationToken);
            }

            updatedLogs += updates.Count;
            ProgressWriter.Write("Ura progress", processedLogs, totalLogs, stopwatch, $"updates {updatedLogs:N0}, unchanged {unchangedLogs:N0}, skipped {skippedLogs:N0}, errors {failedLogs:N0}");
        }

        Console.WriteLine($"Ura progress backfill complete. Logs {processedLogs:N0}/{totalLogs:N0}, updates {updatedLogs:N0}, unchanged {unchangedLogs:N0}, skipped {skippedLogs:N0}, errors {failedLogs:N0}.");
    }

    private static IQueryable<UraProgressRow> BuildUraProgressQuery(DatabaseContext context, ReprocessorOptions options, long? afterFightLogId)
    {
        var query =
            from fightLog in context.FightLog
            join rawData in context.FightLogRawData on fightLog.FightLogId equals rawData.FightLogId
            where fightLog.FightType == (short)FightTypesEnum.Ura
            where rawData.RawFightData != null && rawData.RawFightData != string.Empty
            select new
            {
                fightLog.FightLogId,
                fightLog.FightMode,
                fightLog.FightPercent,
                fightLog.FightPhase,
                rawData.RawFightData
            };

        if (afterFightLogId.HasValue)
        {
            query = query.Where(row => row.FightLogId > afterFightLogId.Value);
        }

        if (options.FromId.HasValue)
        {
            query = query.Where(row => row.FightLogId >= options.FromId.Value);
        }

        if (options.ToId.HasValue)
        {
            query = query.Where(row => row.FightLogId <= options.ToId.Value);
        }

        return query
            .OrderBy(row => row.FightLogId)
            .Select(row => new UraProgressRow(
                row.FightLogId,
                row.FightMode,
                row.FightPercent,
                row.FightPhase,
                row.RawFightData!));
    }

    private static async Task BulkUpdateUraProgressAsync(DatabaseContext context, IReadOnlyList<UraProgressUpdate> updates, CancellationToken cancellationToken)
    {
        var values = new StringBuilder(updates.Count * 32);
        var parameters = new List<object>(updates.Count * 4);

        for (var index = 0; index < updates.Count; index++)
        {
            if (index > 0)
            {
                values.Append(", ");
            }

            values.Append(CultureInfo.InvariantCulture, $"(@id{index}, @mode{index}, @phase{index}, @percent{index})");

            var update = updates[index];
            parameters.Add(new NpgsqlParameter($"id{index}", update.FightLogId));
            parameters.Add(new NpgsqlParameter($"mode{index}", update.FightMode));
            parameters.Add(new NpgsqlParameter($"phase{index}", (object?)update.FightPhase ?? DBNull.Value));
            parameters.Add(new NpgsqlParameter($"percent{index}", update.FightPercent));
        }

        var sql = $$"""
        UPDATE "FightLog" AS fight_log
        SET "FightMode" = updates."FightMode",
            "FightPhase" = updates."FightPhase",
            "FightPercent" = updates."FightPercent"
        FROM (VALUES {{values}}) AS updates("FightLogId", "FightMode", "FightPhase", "FightPercent")
        WHERE fight_log."FightLogId" = updates."FightLogId";
        """;

        await context.Database.ExecuteSqlRawAsync(sql, parameters, cancellationToken);
    }
}

internal static class HarvestTempleProgressBackfillRunner
{
    public static async Task RunAsync(IDbContextFactory<DatabaseContext> factory, ReprocessorOptions options, CancellationToken cancellationToken)
    {
        Console.WriteLine(options.DryRun
            ? "Starting Harvest Temple progress dry run."
            : "Starting Harvest Temple progress backfill.");

        var stopwatch = Stopwatch.StartNew();
        var processedLogs = 0;
        var updatedLogs = 0;
        var unchangedLogs = 0;
        var skippedLogs = 0;
        var failedLogs = 0;

        await using var countContext = await factory.CreateDbContextAsync(cancellationToken);
        var totalLogs = await BuildHarvestTempleProgressQuery(countContext, options, null)
            .CountAsync(cancellationToken);

        Console.WriteLine($"Queued {totalLogs:N0} Harvest Temple fight logs with raw data for progress backfill.");

        long? lastFightLogId = null;

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            List<HarvestTempleProgressRow> fightRows;
            await using (var queryContext = await factory.CreateDbContextAsync(cancellationToken))
            {
                fightRows = await BuildHarvestTempleProgressQuery(queryContext, options, lastFightLogId)
                    .Take(options.BatchSize)
                    .ToListAsync(cancellationToken);
            }

            if (fightRows.Count == 0)
            {
                break;
            }

            lastFightLogId = fightRows[^1].FightLogId;
            var updates = new List<HarvestTempleProgressUpdate>(fightRows.Count);

            foreach (var fightRow in fightRows)
            {
                try
                {
                    var fightData = JsonConvert.DeserializeObject<FightEliteInsightDataModel>(fightRow.RawFightData) ?? new FightEliteInsightDataModel();
                    if (fightData.Targets is not { Count: > 0 })
                    {
                        skippedLogs++;
                        processedLogs++;
                        continue;
                    }

                    var progress = FightLogProgressCalculator.Calculate(fightData, (short)FightTypesEnum.Ht, fightRow.FightMode);
                    if (progress.FightPhase is null)
                    {
                        skippedLogs++;
                        processedLogs++;
                        continue;
                    }

                    if (fightRow.FightPhase == progress.FightPhase &&
                        fightRow.FightPercent == progress.FightPercent)
                    {
                        unchangedLogs++;
                        processedLogs++;
                        continue;
                    }

                    updates.Add(new HarvestTempleProgressUpdate(
                        fightRow.FightLogId,
                        progress.FightPhase,
                        progress.FightPercent));
                    processedLogs++;
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    failedLogs++;
                    Console.Error.WriteLine($"Failed to update Harvest Temple progress for FightLogId {fightRow.FightLogId}: {ex.Message}");
                    processedLogs++;
                }
            }

            if (updates.Count > 0 && options.DryRun is false)
            {
                await using var updateContext = await factory.CreateDbContextAsync(cancellationToken);
                updateContext.ChangeTracker.AutoDetectChangesEnabled = false;
                await BulkUpdateHarvestTempleProgressAsync(updateContext, updates, cancellationToken);
            }

            updatedLogs += updates.Count;
            ProgressWriter.Write("HT progress", processedLogs, totalLogs, stopwatch, $"updates {updatedLogs:N0}, unchanged {unchangedLogs:N0}, skipped {skippedLogs:N0}, errors {failedLogs:N0}");
        }

        Console.WriteLine($"Harvest Temple progress backfill complete. Logs {processedLogs:N0}/{totalLogs:N0}, updates {updatedLogs:N0}, unchanged {unchangedLogs:N0}, skipped {skippedLogs:N0}, errors {failedLogs:N0}.");
    }

    private static IQueryable<HarvestTempleProgressRow> BuildHarvestTempleProgressQuery(DatabaseContext context, ReprocessorOptions options, long? afterFightLogId)
    {
        var query =
            from fightLog in context.FightLog
            join rawData in context.FightLogRawData on fightLog.FightLogId equals rawData.FightLogId
            where fightLog.FightType == (short)FightTypesEnum.Ht
            where rawData.RawFightData != null && rawData.RawFightData != string.Empty
            select new
            {
                fightLog.FightLogId,
                fightLog.FightMode,
                fightLog.FightPercent,
                fightLog.FightPhase,
                rawData.RawFightData
            };

        if (options.Force is false)
        {
            query = query.Where(row =>
                row.FightPhase == null ||
                row.FightPhase < 1 ||
                row.FightPhase > 6 ||
                row.FightPercent < 0m ||
                row.FightPercent > 100m);
        }

        if (afterFightLogId.HasValue)
        {
            query = query.Where(row => row.FightLogId > afterFightLogId.Value);
        }

        if (options.FromId.HasValue)
        {
            query = query.Where(row => row.FightLogId >= options.FromId.Value);
        }

        if (options.ToId.HasValue)
        {
            query = query.Where(row => row.FightLogId <= options.ToId.Value);
        }

        return query
            .OrderBy(row => row.FightLogId)
            .Select(row => new HarvestTempleProgressRow(
                row.FightLogId,
                row.FightMode,
                row.FightPercent,
                row.FightPhase,
                row.RawFightData!));
    }

    private static async Task BulkUpdateHarvestTempleProgressAsync(DatabaseContext context, IReadOnlyList<HarvestTempleProgressUpdate> updates, CancellationToken cancellationToken)
    {
        var values = new StringBuilder(updates.Count * 24);
        var parameters = new List<object>(updates.Count * 3);

        for (var index = 0; index < updates.Count; index++)
        {
            if (index > 0)
            {
                values.Append(", ");
            }

            values.Append(CultureInfo.InvariantCulture, $"(@id{index}, @phase{index}, @percent{index})");

            var update = updates[index];
            parameters.Add(new NpgsqlParameter($"id{index}", update.FightLogId));
            parameters.Add(new NpgsqlParameter($"phase{index}", (object?)update.FightPhase ?? DBNull.Value));
            parameters.Add(new NpgsqlParameter($"percent{index}", update.FightPercent));
        }

        var sql = $$"""
        UPDATE "FightLog" AS fight_log
        SET "FightPhase" = updates."FightPhase",
            "FightPercent" = updates."FightPercent"
        FROM (VALUES {{values}}) AS updates("FightLogId", "FightPhase", "FightPercent")
        WHERE fight_log."FightLogId" = updates."FightLogId";
        """;

        await context.Database.ExecuteSqlRawAsync(sql, parameters, cancellationToken);
    }
}

internal static class ProgressWriter
{
    public static void Write(string label, int processed, int total, Stopwatch stopwatch, string detail)
    {
        var rate = stopwatch.Elapsed.TotalSeconds <= 0
            ? 0
            : processed / stopwatch.Elapsed.TotalSeconds;
        var remaining = Math.Max(0, total - processed);
        var eta = rate <= 0
            ? TimeSpan.Zero
            : TimeSpan.FromSeconds(remaining / rate);

        Console.WriteLine($"{label}: {processed:N0}/{total:N0} logs ({Percent(processed, total)}), {rate:N1} logs/s, ETA {FormatDuration(eta)}; {detail}.");
    }

    private static string Percent(int processed, int total)
    {
        if (total <= 0)
        {
            return "100.0%";
        }

        return $"{processed * 100d / total:N1}%";
    }

    private static string FormatDuration(TimeSpan duration)
    {
        if (duration.TotalHours >= 1)
        {
            return $"{duration.TotalHours:N1}h";
        }

        if (duration.TotalMinutes >= 1)
        {
            return $"{duration.TotalMinutes:N1}m";
        }

        return $"{duration.TotalSeconds:N0}s";
    }
}

internal sealed class DatabaseContextFactory(DbContextOptions<DatabaseContext> options) : IDbContextFactory<DatabaseContext>
{
    public DatabaseContext CreateDbContext()
    {
        return new DatabaseContext(options);
    }

    public Task<DatabaseContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(CreateDbContext());
    }
}

internal sealed record FightRawBatchRow(
    long FightLogId,
    short FightType,
    long FightDuration,
    string RawFightData,
    string? RawHealingData,
    string? RawBarrierData);

internal sealed record PlayerLogLite(
    long PlayerFightLogId,
    long FightLogId,
    string GuildWarsAccountName);

internal sealed record PlayerLogUpdate(
    long PlayerFightLogId,
    decimal StabGenOnGroup,
    decimal StabGenOffGroup,
    decimal QuicknessGenGroup,
    decimal AlacGenGroup,
    string BoonRole,
    string Playstyle);

internal sealed record UraProgressRow(
    long FightLogId,
    int FightMode,
    decimal FightPercent,
    int? FightPhase,
    string RawFightData);

internal sealed record UraProgressUpdate(
    long FightLogId,
    int FightMode,
    int? FightPhase,
    decimal FightPercent);

internal sealed record HarvestTempleProgressRow(
    long FightLogId,
    int FightMode,
    decimal FightPercent,
    int? FightPhase,
    string RawFightData);

internal sealed record HarvestTempleProgressUpdate(
    long FightLogId,
    int? FightPhase,
    decimal FightPercent);
