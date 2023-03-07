using Models;
using Newtonsoft.Json;

namespace Services.DiscordMessagingServices
{
    public class DataModelGenerationService : IDataModelGenerationService
    {
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
            EliteInsightDataModel deserializeData = new EliteInsightDataModel();
            try
            {
                deserializeData = JsonConvert.DeserializeObject<EliteInsightDataModel>(data) ??
                                  new EliteInsightDataModel();
            }
            catch (Exception ex)
            {
                var message = ex.Message;
            }

            deserializeData.Url = url;
            return deserializeData;
        }
    }
}