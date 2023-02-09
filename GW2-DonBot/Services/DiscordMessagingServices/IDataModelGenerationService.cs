using Models;

namespace Services.DiscordMessagingServices
{
    public interface IDataModelGenerationService
    {
        public Task<EliteInsightDataModel> GenerateEliteInsightDataModelFromUrl(string url);
    }
}