using Discord.WebSocket;
using DonBot.Extensions;
using DonBot.Models.Enums;
using DonBot.Models.Statics;
using DonBot.Services.DatabaseServices;

namespace DonBot.Services.GuildWarsServices;

public sealed class FightLogService(IDataModelGenerationService dataModelGenerationService, IEntityService entityService) : IFightLogService
{
    public async Task GetEnemyInformation(SocketMessageComponent command)
    {
        await command.DeferAsync(ephemeral: true);
        var url = command.Message.Embeds.FirstOrDefault()?.Url;
        if (url == null)
        {
            await command.FollowupAsync("Unable to get the url from the requested log", ephemeral: true);
            return;
        }

        var data = await dataModelGenerationService.GenerateEliteInsightDataModelFromUrl(url);
        if (data.FightEliteInsightDataModel.Targets == null)
        {
            await command.FollowupAsync("Unable to get enemy targets from the requested log", ephemeral: true);
            return;
        }
        var targets = data.FightEliteInsightDataModel.Targets.Where(s => s.Name != "Dummy PvP Agent").ToList();

        var targetsByClass = targets.GroupBy(s => s.Name?.Split(' ').FirstOrDefault());

        var enemyOverview = "```Class         Count  Avg Dmg   Strike   Condi\n";

        foreach (var classTarget in targetsByClass.OrderByDescending(s => s.Average(d => d.Details?.DmgDistributions?.Sum(dis => dis.TotalDamage))))
        {
            var averageStrikeDamage = classTarget.Average(s => s.Details?.DmgDistributions?.Sum(d => d.Distribution?.Where(dis => dis[0].Bool == false).Sum(dis => dis[2].Double))) ?? 0;
            var averageCondiDamage = classTarget.Average(s => s.Details?.DmgDistributions?.Sum(d => d.Distribution?.Where(dis => dis[0].Bool == true).Sum(dis => dis[2].Double))) ?? 0;
            var totalAverageDamage = averageStrikeDamage + averageCondiDamage;
            enemyOverview += $"{classTarget.Key?.PadRight(12)}{string.Empty, -2}{classTarget.Count(),-5}{string.Empty, -2}{((float)totalAverageDamage).FormatNumber(),-7}{string.Empty, -3}{((float)averageStrikeDamage).FormatNumber(),-7}{string.Empty, -2}{((float)averageCondiDamage).FormatNumber(),-7}\n";
        }
        enemyOverview += "```";

        await command.FollowupAsync($"**Know My Enemy**{Environment.NewLine}{data.FightEliteInsightDataModel.Url}{Environment.NewLine}{enemyOverview}", ephemeral: true);
    }

    public async Task GetBestTimes(SocketMessageComponent command)
    {
        await command.DeferAsync(ephemeral: true);

        var fightsReportId = long.Parse(command.Data.CustomId[ButtonId.BestTimesPvEPrefix.Length..]);
        var fightsReport = await entityService.FightsReport.GetFirstOrDefaultAsync(r => r.FightsReportId == fightsReportId);
        if (fightsReport == null)
        {
            await command.FollowupAsync("Session data not found.", ephemeral: true);
            return;
        }

        var sessionFights = await entityService.FightLog.GetWhereAsync(s =>
            s.GuildId == fightsReport.GuildId &&
            s.FightStart >= fightsReport.FightsStart &&
            s.FightStart <= fightsReport.FightsEnd &&
            s.FightType != (short)FightTypesEnum.WvW &&
            s.FightType != (short)FightTypesEnum.Unkn &&
            s.FightType != (short)FightTypesEnum.Golem);

        var sessionCombos = sessionFights
            .Select(f => (f.FightType, f.FightMode))
            .ToHashSet();

        if (sessionCombos.Count == 0)
        {
            await command.FollowupAsync("No PvE fights found in this session.", ephemeral: true);
            return;
        }

        var historicalFights = await entityService.FightLog.GetWhereAsync(s =>
            s.GuildId == fightsReport.GuildId &&
            s.IsSuccess &&
            s.FightType != (short)FightTypesEnum.WvW &&
            s.FightType != (short)FightTypesEnum.Unkn &&
            s.FightType != (short)FightTypesEnum.Golem);

        var bestTimes = historicalFights
            .Where(f => sessionCombos.Contains((f.FightType, f.FightMode)))
            .GroupBy(f => (f.FightType, f.FightMode))
            .Select(g => g.OrderBy(f => f.FightDurationInMs).First())
            .OrderBy(x => x.FightType)
            .ThenBy(x => x.FightMode)
            .ToList();

        if (!bestTimes.Any())
        {
            await command.FollowupAsync("No successful records found for the fights in this session.", ephemeral: true);
            return;
        }

        const int maxLength = 2000;
        var header = "**PvE Best Times**\n";
        var chunks = new List<string>();
        var current = new System.Text.StringBuilder(header);

        foreach (var fight in bestTimes)
        {
            var bossName = Enum.GetName(typeof(FightTypesEnum), fight.FightType) ?? "Unknown";
            var modeName = fight.FightMode.GetFightModeName();
            var time = TimeSpan.FromMilliseconds(fight.FightDurationInMs);
            var timeString = $"{(time.Hours * 60) + time.Minutes:D2}m:{time.Seconds:D2}s";
            var line = $"**{bossName} ({modeName})** - `{timeString}` - [view log]({fight.Url})\n";

            if (current.Length + line.Length > maxLength)
            {
                chunks.Add(current.ToString());
                current = new System.Text.StringBuilder(line);
            }
            else
            {
                current.Append(line);
            }
        }

        if (current.Length > 0)
        {
            chunks.Add(current.ToString());
        }

        foreach (var chunk in chunks)
        {
            await command.FollowupAsync(chunk, ephemeral: true);
        }
    }
}