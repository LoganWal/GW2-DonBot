using DonBot.Core.Models.Entities;
using DonBot.Core.Models.Enums;
using DonBot.Core.Models.GuildWars2;
using DonBot.Core.Services;
using Microsoft.EntityFrameworkCore;

namespace DonBot.Core.Services.GuildWars2;

public enum ExistingFightLogUpdateMode
{
    RawDataOnly,
    AttachGuildAndRawData,
    RefreshMetadataAndRawData
}

public sealed record FightLogIngestionRequest(
    EliteInsightDataModel Data,
    ArcDpsPhase FightPhase,
    IReadOnlyList<Gw2Player> Players)
{
    public long GuildId { get; init; }

    public ExistingFightLogUpdateMode ExistingLogUpdateMode { get; init; } =
        ExistingFightLogUpdateMode.AttachGuildAndRawData;

    public string SourceFallback { get; init; } = "unknown";
}

public sealed record FightLogIngestionResult(
    long FightLogId,
    FightLog FightLog,
    bool Created);

public sealed class FightLogIngestionService(IDbContextFactory<DatabaseContext> dbContextFactory)
{
    public async Task<FightLogIngestionResult> IngestAsync(
        FightLogIngestionRequest request,
        CancellationToken ct = default)
    {
        await using var ctx = await dbContextFactory.CreateDbContextAsync(ct);
        var materialization = FightLogMaterializer.Materialize(
            request.Data,
            request.FightPhase,
            request.GuildId,
            request.SourceFallback);

        var existing = await FindExistingAsync(ctx, request, materialization, ct);
        if (existing != null)
        {
            await ApplyExistingAsync(ctx, existing, request, materialization, ct);
            await FightLogRawDataStore.UpsertAsync(ctx, existing.FightLogId, request.Data, ct);
            if (request.ExistingLogUpdateMode == ExistingFightLogUpdateMode.RefreshMetadataAndRawData)
            {
                await UpsertPlayerFightLogsAsync(ctx, existing, request, ct);
            }
            return new FightLogIngestionResult(existing.FightLogId, existing, Created: false);
        }

        var fightLog = FightLogMaterializer.CreateFightLog(request.Data, materialization);
        ctx.FightLog.Add(fightLog);
        await ctx.SaveChangesAsync(ct);

        await FightLogRawDataStore.UpsertAsync(ctx, fightLog.FightLogId, request.Data, ct);
        await UpsertPlayerFightLogsAsync(ctx, fightLog, request, ct);

        return new FightLogIngestionResult(fightLog.FightLogId, fightLog, Created: true);
    }

    private static async Task<FightLog?> FindExistingAsync(
        DatabaseContext ctx,
        FightLogIngestionRequest request,
        FightLogMaterialization materialization,
        CancellationToken ct)
    {
        var url = request.Data.FightEliteInsightDataModel.Url;
        if (!string.IsNullOrEmpty(url))
        {
            var existingByUrl = await ctx.FightLog.FirstOrDefaultAsync(f => f.Url == url, ct);
            if (existingByUrl != null)
            {
                return existingByUrl;
            }
        }

        return await FightLogDeduplication.FindByContentAsync(
            ctx,
            materialization.FightType,
            materialization.FightStart,
            request.Players.Select(player => player.AccountName),
            ct);
    }

    private static async Task ApplyExistingAsync(
        DatabaseContext ctx,
        FightLog existing,
        FightLogIngestionRequest request,
        FightLogMaterialization materialization,
        CancellationToken ct)
    {
        switch (request.ExistingLogUpdateMode)
        {
            case ExistingFightLogUpdateMode.RawDataOnly:
                return;
            case ExistingFightLogUpdateMode.AttachGuildAndRawData:
                if (request.GuildId <= 0 || existing.GuildId != 0)
                {
                    return;
                }

                existing.GuildId = request.GuildId;
                break;
            case ExistingFightLogUpdateMode.RefreshMetadataAndRawData:
                FightLogMaterializer.ApplyToExisting(existing, materialization);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(request), request.ExistingLogUpdateMode, "Unknown existing log update mode.");
        }

        ctx.FightLog.Update(existing);
        await ctx.SaveChangesAsync(ct);
    }

    private static async Task UpsertPlayerFightLogsAsync(
        DatabaseContext ctx,
        FightLog fightLog,
        FightLogIngestionRequest request,
        CancellationToken ct)
    {
        var playerFightLogs = request.Data.FightEliteInsightDataModel.Wvw
            ? PlayerFightLogFactory.CreateWvw(request.Players, fightLog.FightLogId, fightLog.FightDurationInMs)
            : PlayerFightLogFactory.CreatePve(request.Players, fightLog.FightLogId, fightLog.FightDurationInMs);
        if (playerFightLogs.Count == 0)
        {
            return;
        }

        var existingPlayerLogs = await ctx.PlayerFightLog
            .AsTracking()
            .Where(p => p.FightLogId == fightLog.FightLogId)
            .ToListAsync(ct);
        var existingByAccountName = existingPlayerLogs
            .Where(p => !string.IsNullOrWhiteSpace(p.GuildWarsAccountName))
            .GroupBy(p => p.GuildWarsAccountName, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
        var matchedExistingIds = new HashSet<long>();
        var currentPlayerLogs = new List<PlayerFightLog>(playerFightLogs.Count);

        foreach (var playerFightLog in playerFightLogs)
        {
            if (existingByAccountName.TryGetValue(playerFightLog.GuildWarsAccountName, out var existingPlayerLog) &&
                matchedExistingIds.Add(existingPlayerLog.PlayerFightLogId))
            {
                ApplyPlayerFightLogValues(ctx, existingPlayerLog, playerFightLog);
                currentPlayerLogs.Add(existingPlayerLog);
                continue;
            }

            ctx.PlayerFightLog.Add(playerFightLog);
            currentPlayerLogs.Add(playerFightLog);
        }

        await ctx.SaveChangesAsync(ct);

        if (request.Data.FightEliteInsightDataModel.Wvw is false)
        {
            await RefreshMechanicsAsync(ctx, request.Players, currentPlayerLogs, ct);
        }
    }

    private static void ApplyPlayerFightLogValues(
        DatabaseContext ctx,
        PlayerFightLog existing,
        PlayerFightLog updated)
    {
        var entry = ctx.Entry(existing);
        foreach (var property in entry.Metadata.GetProperties())
        {
            if (property.Name == nameof(PlayerFightLog.PlayerFightLogId) ||
                property.PropertyInfo is null)
            {
                continue;
            }

            entry.Property(property.Name).CurrentValue = property.PropertyInfo.GetValue(updated);
        }
    }

    private static async Task RefreshMechanicsAsync(
        DatabaseContext ctx,
        IReadOnlyList<Gw2Player> players,
        IReadOnlyList<PlayerFightLog> playerFightLogs,
        CancellationToken ct)
    {
        var playerFightLogIds = playerFightLogs
            .Select(playerFightLog => playerFightLog.PlayerFightLogId)
            .Where(id => id > 0)
            .Distinct()
            .ToList();
        if (playerFightLogIds.Count == 0)
        {
            return;
        }

        var existingMechanics = await ctx.PlayerFightLogMechanic
            .Where(mechanic => playerFightLogIds.Contains(mechanic.PlayerFightLogId))
            .ToListAsync(ct);
        if (existingMechanics.Count > 0)
        {
            ctx.PlayerFightLogMechanic.RemoveRange(existingMechanics);
            await ctx.SaveChangesAsync(ct);
        }

        await AttachMechanicsAsync(ctx, players, playerFightLogs, ct);
    }

    private static async Task AttachMechanicsAsync(
        DatabaseContext ctx,
        IReadOnlyList<Gw2Player> players,
        IReadOnlyList<PlayerFightLog> playerFightLogs,
        CancellationToken ct)
    {
        var mechanicRecords = playerFightLogs
            .SelectMany(playerFightLog =>
            {
                var player = players.FirstOrDefault(p =>
                    string.Equals(p.AccountName, playerFightLog.GuildWarsAccountName, StringComparison.OrdinalIgnoreCase));
                return player?.Mechanics.Select(mechanic => new PlayerFightLogMechanic
                {
                    PlayerFightLogId = playerFightLog.PlayerFightLogId,
                    MechanicName = mechanic.Key,
                    MechanicCount = mechanic.Value
                }) ?? [];
            })
            .ToList();

        if (mechanicRecords.Count == 0)
        {
            return;
        }

        ctx.PlayerFightLogMechanic.AddRange(mechanicRecords);
        await ctx.SaveChangesAsync(ct);
    }
}
