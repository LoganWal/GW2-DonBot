using System.Globalization;
using Discord;
using DonBot.Core.Models.GuildWars2;
using DonBot.Core.Services.GuildWars2;
using DonBot.Extensions;
using Microsoft.Extensions.Configuration;

namespace DonBot.Services.GuildWarsServices.MessageGeneration;

public sealed class PvEFightSummaryService(
    IFooterService footerService,
    IPlayerService playerService,
    IRotationAnalysisService rotationAnalysisService,
    IPointsAwardService pointsAwardService,
    IConfiguration configuration,
    FightLogIngestionService fightLogIngestionService) : IPvEFightSummaryService
{
    public async Task<(Embed Embed, string? WebAppUrl, long FightLogId)> GenerateSimple(EliteInsightDataModel data, long guildId)
    {
        // Runs in the background; failures must not block the summary.
        _ = Task.Run(async () =>
        {
            try
            {
                await rotationAnalysisService.AnalyzePlayerRotations(data);
            }
            catch (Exception ex) { _ = ex; }
        });

        var fightPhase = FightLogMaterializer.ResolveFightPhase(data);
        var gw2Players = playerService.GetGw2Players(data, fightPhase, FightLogMaterializer.ShouldSumAllTargets(data));
        var ingestionResult = await fightLogIngestionService.IngestAsync(new FightLogIngestionRequest(data, fightPhase, gw2Players)
        {
            GuildId = guildId,
            ExistingLogUpdateMode = ExistingFightLogUpdateMode.RefreshMetadataAndRawData
        });
        var fightLog = ingestionResult.FightLog;

        await pointsAwardService.AwardFightAsync(fightLog.FightLogId);

        var webAppBaseUrl = configuration["WebApp:BaseUrl"];
        var webAppUrl = !string.IsNullOrEmpty(webAppBaseUrl)
            ? $"{webAppBaseUrl}/logs/{fightLog.FightLogId}"
            : null;
        var isSuccess = data.FightEliteInsightDataModel.Success ?? fightPhase.Success ?? false;
        var wipeProgressText = isSuccess
            ? string.Empty
            : $" {FormatFightProgress(fightLog.FightPhase, fightLog.FightPercent)}";

        var message = new EmbedBuilder
        {
            Title = $"{data.FightEliteInsightDataModel.LogName}\n",
            Description = $"**Length:** {data.FightEliteInsightDataModel.Phases?.FirstOrDefault()?.EncounterDuration}{wipeProgressText}\n",
            Color = isSuccess ? Color.Green : Color.Red,
            Author = new EmbedAuthorBuilder()
            {
                Name = "GW2-DonBot",
                Url = "https://github.com/LoganWal/GW2-DonBot",
                IconUrl = "https://i.imgur.com/tQ4LD6H.png"
            },
            Url = $"{data.FightEliteInsightDataModel.Url}",
            Footer = new EmbedFooterBuilder()
            {
                Text = $"{await footerService.Generate(guildId)}",
                IconUrl = "https://i.imgur.com/tQ4LD6H.png"
            },
            Timestamp = DateTime.Now
        };

        var fightInSeconds = fightLog.FightDurationInMs / 1000f;
        var playerOverview = "```Player         Dmg       Cleave    Alac    Quick                                                         \n";
        foreach (var gw2Player in gw2Players.OrderByDescending(s => s.Damage))
        {
            playerOverview += $"{gw2Player.AccountName.ClipAt(13),-13}{string.Empty,2}{(gw2Player.Damage / (fightInSeconds)).FormatNumber(true),-8}{string.Empty,2}{(gw2Player.Cleave / (fightInSeconds)).FormatNumber(true),-8}{string.Empty,2}{Math.Round(gw2Player.TotalAlac, 2).ToString(CultureInfo.CurrentCulture),-5}{string.Empty,3}{Math.Round(gw2Player.TotalQuick, 2).ToString(CultureInfo.CurrentCulture),-5}\n";
        }

        playerOverview += "```";

        message.AddField(x =>
        {
            x.Name = "Player Overview";
            x.Value = $"{playerOverview}";
            x.IsInline = false;
        });

        var survivabilityOverview = "```Player         Res (s)    Dmg Taken   Downed                                            \n";
        foreach (var gw2Player in gw2Players.OrderBy(s => s.ResurrectionTime))
        {
            survivabilityOverview += $"{gw2Player.AccountName.ClipAt(13),-13}{string.Empty,2}{Math.Round((double)gw2Player.ResurrectionTime / 1000, 3),-9}{string.Empty,2}{(gw2Player.DamageTaken),-10}{string.Empty,2}{gw2Player.TimesDowned}\n";
        }

        survivabilityOverview += "```";

        message.AddField(x =>
        {
            x.Name = "Survivability Overview";
            x.Value = $"{survivabilityOverview}";
            x.IsInline = false;
        });

        footerService.AddWidthSpacer(message);
        footerService.AddInviteLink(message);

        return (message.Build(), webAppUrl, fightLog.FightLogId);
    }

    private static string FormatFightProgress(int? fightPhase, decimal fightPercent) =>
        fightPhase.HasValue
            ? $"P{fightPhase.Value} - {fightPercent}%"
            : $"{fightPercent}%";
}
