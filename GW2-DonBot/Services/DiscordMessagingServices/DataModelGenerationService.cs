using Models;
using HtmlAgilityPack;
using Newtonsoft.Json;

namespace Services.DiscordMessagingServices
{
    public class DataModelGenerationService : IDataModelGenerationService
    {
        public EliteInsightDataModel GenerateEliteInsightDataModelFromUrl(string url)
        {
            // HTML scraping
            var web = new HtmlWeb();
            var htmlDoc = web.Load(url);

            // Registering start and end of actual log data inside the HTML
            var logDataStartIndex = htmlDoc.ParsedText.IndexOf("logData", StringComparison.Ordinal) + 10;
            var logDataEndIndex = htmlDoc.ParsedText.IndexOf("};", StringComparison.Ordinal);

            var data = htmlDoc.ParsedText.Substring(logDataStartIndex, logDataEndIndex - logDataStartIndex + 1);

            // Deserializing back to the data model
            return JsonConvert.DeserializeObject<EliteInsightDataModel>(data) ?? new EliteInsightDataModel();
        }
    }
}