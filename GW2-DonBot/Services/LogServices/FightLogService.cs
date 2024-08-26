using Discord.WebSocket;
using Extensions;
using Services.LogGenerationServices;
using Services.PlayerServices;

namespace Services.Logging
{
    public class FightLogService : IFightLogService
    {
        private readonly IDataModelGenerationService _dataModelGenerationService;

        public FightLogService(IDataModelGenerationService dataModelGenerationService)
        {
            _dataModelGenerationService = dataModelGenerationService;
        }

        public async Task GetEnemyInformation(SocketMessageComponent command)
        {
            var url = command.Message.Embeds.FirstOrDefault()?.Url;
            if (url == null)
            {
                await command.RespondAsync("Unable to get the url from the requested log", ephemeral: true);
                return;
            }

            var data = await _dataModelGenerationService.GenerateEliteInsightDataModelFromUrl(url);
            if (data.Targets == null)
            {
                await command.RespondAsync("Unable to get enemy targets from the requested log", ephemeral: true);
                return;
            }
            var targets = data.Targets.Where(s => s.Name != "Dummy PvP Agent").ToList();

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

            await command.RespondAsync($"**Know My Enemy**{Environment.NewLine}{data.Url}{Environment.NewLine}{enemyOverview}", ephemeral: true);
        }
    }
}
