using Testing.Models;
using HtmlAgilityPack;
using Newtonsoft.Json;

namespace Services.DiscordBase
{
    public class DataModelGenerationService
    {
        public EliteInsightDataModel GenerateEliteInsightDataModelFromUrl(BotSecretsDataModel secrets, string url)
        {
            // HTML scraping
            HtmlWeb web = new HtmlWeb();
            var htmlDoc = web.Load(url);

            // Registering start and end of actual log data inside the HTML
            var logDataStartIndex = htmlDoc.ParsedText.IndexOf("logData") + 10;
            var logDataEndIndex = htmlDoc.ParsedText.IndexOf("};");

            var data = htmlDoc.ParsedText.Substring(logDataStartIndex, (logDataEndIndex - logDataStartIndex) + 1);

            // Deserializing back to the data model
            var parsedData = JsonConvert.DeserializeObject<EliteInsightDataModel>(data);
            return parsedData;
        }
    }
}