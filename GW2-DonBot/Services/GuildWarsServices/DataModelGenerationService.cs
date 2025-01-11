using DonBot.Models.GuildWars2;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace DonBot.Services.GuildWarsServices
{
    public class DataModelGenerationService(
        ILogger<DataModelGenerationService> logger,
        IHttpClientFactory httpClientFactory)
        : IDataModelGenerationService
    {
        public async Task<EliteInsightDataModel> GenerateEliteInsightDataModelFromUrl(string url)
        {
            using var response = await httpClientFactory.CreateClient().GetAsync(url);
            using var content = response.Content;
            var result = await content.ReadAsStringAsync();

            // Registering start and end of actual log data inside the HTML
            var logDataStartIndex = result.IndexOf("logData", StringComparison.Ordinal) + 10;
            var logDataEndIndex = result.IndexOf("};", StringComparison.Ordinal);

            var data = result.Substring(logDataStartIndex, logDataEndIndex - logDataStartIndex + 1);

            // Deserializing back to the data model
            var deserializeData = new EliteInsightDataModel();
            try
            {
                deserializeData = JsonConvert.DeserializeObject<EliteInsightDataModel>(data) ?? new EliteInsightDataModel();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to create data model from guild wars 2 log");
            }

            deserializeData.Url = url;
            return deserializeData;
        }
    }
}