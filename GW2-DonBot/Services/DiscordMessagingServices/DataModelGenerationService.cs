using HtmlAgilityPack;
using Models;
using Newtonsoft.Json;

namespace Services.DiscordMessagingServices
{
    public class DataModelGenerationService : IDataModelGenerationService
    {
        public async Task<EliteInsightDataModel> GenerateEliteInsightDataModelFromUrl(string url)
        {
            // HTML scraping
            //var web = new HtmlWeb();
            //var htmlDoc = new System.Net.WebClient().DownloadString(url); //await web.LoadFromWebAsync(url);
            string result;

            using (HttpClient client = new HttpClient())
            {
                using (HttpResponseMessage response = client.GetAsync(url).Result)
                {
                    using (HttpContent content = response.Content)
                    {
                        result = content.ReadAsStringAsync().Result;
                    }
                }
            }

            // Registering start and end of actual log data inside the HTML
            var logDataStartIndex = result.IndexOf("logData", StringComparison.Ordinal) + 10;
            var logDataEndIndex = result.IndexOf("};", StringComparison.Ordinal);

            var data = result.Substring(logDataStartIndex, logDataEndIndex - logDataStartIndex + 1);

            // Deserializing back to the data model
            var deserializeData = JsonConvert.DeserializeObject<EliteInsightDataModel>(data) ?? new EliteInsightDataModel();
            deserializeData.Url = url;
            return deserializeData;
        }
    }
}