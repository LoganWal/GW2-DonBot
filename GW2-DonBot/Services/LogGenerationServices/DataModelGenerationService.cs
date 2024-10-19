using Microsoft.Extensions.Logging;
using Models;
using Newtonsoft.Json;
using Services.DiscordRequestServices;

namespace Services.LogGenerationServices
{
    public class DataModelGenerationService : IDataModelGenerationService
    {
        private readonly ILogger<DataModelGenerationService> _logger;

        public DataModelGenerationService(ILogger<DataModelGenerationService> logger)
        {
            _logger = logger;
        }

        public async Task<EliteInsightDataModel> GenerateEliteInsightDataModelFromUrl(string url)
        {
            // HTML scraping
            string result;

            using (var client = new HttpClient())
            {
                using var response = await client.GetAsync(url);
                using var content = response.Content;
                result = await content.ReadAsStringAsync();
            }

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
                _logger.LogError(ex, "Failed to create data model from guild wars 2 log");
            }

            deserializeData.Url = url;
            return deserializeData;
        }
    }
}