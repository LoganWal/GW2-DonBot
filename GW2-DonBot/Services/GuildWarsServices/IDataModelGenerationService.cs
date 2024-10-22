using DonBot.Models;
using DonBot.Models.GuildWars2;

namespace DonBot.Services.LogGenerationServices
{
    public interface IDataModelGenerationService
    {
        public Task<EliteInsightDataModel> GenerateEliteInsightDataModelFromUrl(string url);
    }
}