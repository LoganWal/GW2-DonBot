using System.Globalization;
using DonBot.Core.Models.Entities;
using DonBot.Core.Models.Enums;
using DonBot.Core.Models.GuildWars2;
using DonBot.Core.Services;

namespace DonBot.Core.Services.GuildWars2;

public sealed record FightLogMaterialization(
    long GuildId,
    short FightType,
    DateTime FightStart,
    long FightDurationInMs,
    bool IsSuccess,
    decimal FightPercent,
    int? FightPhase,
    int FightMode,
    string? Source);

public static class FightLogMaterializer
{
    private const string EliteInsightsDateFormat = "yyyy-MM-dd HH:mm:ss zzz";

    public static ArcDpsPhase ResolveFightPhase(EliteInsightDataModel data) =>
        data.FightEliteInsightDataModel.Phases?.FirstOrDefault() ?? new ArcDpsPhase();

    public static bool ShouldSumAllTargets(EliteInsightDataModel data) =>
        data.FightEliteInsightDataModel.Wvw ||
        EncounterCatalog.ResolvePveEncounter(data.FightEliteInsightDataModel.FightId).SumAllTargets;

    public static FightLogMaterialization Materialize(
        EliteInsightDataModel data,
        ArcDpsPhase fightPhase,
        long guildId,
        string sourceFallback = "unknown")
    {
        var fightData = data.FightEliteInsightDataModel;
        var fightStart = ParseStart(fightData.Start);
        var fightType = fightData.Wvw
            ? (short)FightTypesEnum.WvW
            : EncounterCatalog.ResolvePveEncounter(fightData.FightId).FightType;
        var fightDuration = fightData.Wvw
            ? ResolveWvwDuration(fightData, fightPhase, fightStart)
            : fightPhase.Duration;
        var fightMode = fightData.Wvw
            ? 0
            : FightLogProgressCalculator.ResolveFightMode(fightData, fightPhase);
        var progress = fightData.Wvw
            ? new FightLogProgress(0m, null)
            : FightLogProgressCalculator.Calculate(fightData, fightType, fightMode);

        return new FightLogMaterialization(
            guildId,
            fightType,
            fightStart,
            fightDuration,
            fightData.Success ?? fightPhase.Success ?? false,
            progress.FightPercent,
            progress.FightPhase,
            fightMode,
            ReportUrlHelper.GetLogSource(fightData.Url, sourceFallback));
    }

    public static FightLog CreateFightLog(EliteInsightDataModel data, FightLogMaterialization materialization) => new()
    {
        GuildId = materialization.GuildId,
        Url = data.FightEliteInsightDataModel.Url,
        FightType = materialization.FightType,
        FightStart = materialization.FightStart,
        FightDurationInMs = materialization.FightDurationInMs,
        IsSuccess = materialization.IsSuccess,
        FightPercent = materialization.FightPercent,
        FightPhase = materialization.FightPhase,
        FightMode = materialization.FightMode,
        Source = materialization.Source
    };

    public static void ApplyToExisting(FightLog fightLog, FightLogMaterialization materialization)
    {
        fightLog.GuildId = materialization.GuildId;
        fightLog.FightType = materialization.FightType;
        fightLog.FightStart = materialization.FightStart;
        fightLog.FightDurationInMs = materialization.FightDurationInMs;
        fightLog.IsSuccess = materialization.IsSuccess;
        fightLog.FightPercent = materialization.FightPercent;
        fightLog.FightPhase = materialization.FightPhase;
        fightLog.FightMode = materialization.FightMode;
        fightLog.Source = materialization.Source;
    }

    private static DateTime ParseStart(string? dateStartString) =>
        string.IsNullOrEmpty(dateStartString)
            ? DateTime.UtcNow
            : DateTime.ParseExact(dateStartString, EliteInsightsDateFormat, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal);

    private static long ResolveWvwDuration(FightEliteInsightDataModel fightData, ArcDpsPhase fightPhase, DateTime fightStart)
    {
        if (!string.IsNullOrEmpty(fightData.End))
        {
            var fightEnd = DateTime.ParseExact(fightData.End, EliteInsightsDateFormat, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal);
            return (long)(fightEnd - fightStart).TotalMilliseconds;
        }

        return fightPhase.Duration;
    }
}
