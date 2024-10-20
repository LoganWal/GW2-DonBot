using DonBot.Models;

namespace DonBot.Services.LogGenerationServices
{
    public interface IDataModelGenerationService
    {
        public Task<EliteInsightDataModel> GenerateEliteInsightDataModelFromUrl(string url);
    }
}