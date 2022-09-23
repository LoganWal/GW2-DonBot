using Models;

namespace Services.DiscordMessagingServices
{
    public interface IDataModelGenerationService
    {
        public EliteInsightDataModel GenerateEliteInsightDataModelFromUrl(string url);
    }
}