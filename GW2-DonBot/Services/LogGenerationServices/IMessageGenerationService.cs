using Discord;
using Models;
using Models.Entities;

namespace Services.LogGenerationServices
{
    public interface IMessageGenerationService
    {
        public Embed GenerateWvWFightSummary(EliteInsightDataModel data, bool advancedLog, Guild guild);

        public Task<Embed> GenerateWvWPlayerReport();

        public Embed GeneratePvEFightSummary(EliteInsightDataModel data);

        public Task<Embed> GenerateWvWPlayerSummary(Guild gw2Guild);
    }
}
